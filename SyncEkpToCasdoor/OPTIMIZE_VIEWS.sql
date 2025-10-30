-- =====================================================
-- EKP 视图优化脚本 - 适配 Casdoor 同步
-- 创建日期: 2025-10-30
-- 说明: 优化现有视图结构,提高数据可靠性和容错性
-- =====================================================

USE [ekp];
GO

-- =====================================================
-- 1. 优化用户同步视图 (移除不可靠的 groups 字段)
-- =====================================================
PRINT '正在优化 vw_casdoor_users_sync 视图...';
GO

ALTER VIEW [dbo].[vw_casdoor_users_sync] AS
WITH elem AS (
    SELECT 
        fd_id AS ElemId,
        fd_name AS ElemName,
        fd_org_type AS OrgType,
        fd_parentid AS ParentId,
        fd_parentorgid AS ParentOrgId
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
),
person AS (
    SELECT 
        e.ElemId AS PersonId,
        e.ElemName AS PersonName,
        p.fd_login_name AS LoginName,
        p.fd_email AS Email,
        p.fd_mobile_no AS MobileNo,
        p.fd_sex AS Sex
    FROM elem e
    INNER JOIN dbo.sys_org_person p ON e.ElemId = p.fd_id
    WHERE e.OrgType = 8  -- 人员类型
      AND p.fd_login_name IS NOT NULL
),
post AS (
    SELECT 
        ElemId AS PostId,
        ParentId,
        ParentOrgId
    FROM elem
    WHERE OrgType = 4  -- 岗位
),
dept AS (
    SELECT 
        ElemId AS DeptId,
        ElemName AS DeptName
    FROM elem
    WHERE OrgType IN (1, 2)  -- 部门和公司
),
company AS (
    SELECT 
        ElemId AS CompanyId,
        ElemName AS CompanyName
    FROM elem
    WHERE OrgType = 1  -- 公司类型
),
person_dept AS (
    -- 优先使用岗位关联得到部门,若没有岗位,退回个人的直接父级部门
    SELECT 
        x.PersonId,
        x.DeptId,
        ROW_NUMBER() OVER (PARTITION BY x.PersonId ORDER BY (SELECT 1)) AS RowNum
    FROM (
        SELECT DISTINCT p.PersonId, COALESCE(po.ParentOrgId, po.ParentId) AS DeptId
        FROM person p
        INNER JOIN dbo.sys_org_post_person spp ON spp.fd_personid = p.PersonId
        INNER JOIN post po ON po.PostId = spp.fd_postid
        
        UNION
        
        SELECT p.PersonId, COALESCE(pe.ParentOrgId, pe.ParentId) AS DeptId
        FROM person p
        INNER JOIN elem pe ON pe.ElemId = p.PersonId AND pe.OrgType = 8
        WHERE NOT EXISTS (SELECT 1 FROM dbo.sys_org_post_person spp WHERE spp.fd_personid = p.PersonId)
    ) AS x
),
dept_company AS (
    SELECT DISTINCT
        d.DeptId,
        COALESCE(
            (SELECT TOP 1 c.CompanyId FROM company c WHERE c.CompanyId = d.DeptId),
            (SELECT TOP 1 se.fd_parentorgid 
             FROM dbo.sys_org_element se 
             WHERE se.fd_id = d.DeptId AND se.fd_parentorgid IN (SELECT CompanyId FROM company))
        ) AS CompanyId
    FROM dept d
),
seed AS (
    SELECT 
        d.DeptId,
        d.DeptName,
        CAST(d.DeptId AS nvarchar(MAX)) AS Path,
        0 AS Level
    FROM dept d
    WHERE d.DeptId IN (SELECT CompanyId FROM company)
    
    UNION ALL
    
    SELECT 
        e.fd_id,
        e.fd_name,
        s.Path,
        s.Level + 1
    FROM dbo.sys_org_element e
    INNER JOIN seed s ON (e.fd_parentid = s.DeptId OR e.fd_parentorgid = s.DeptId)
    WHERE e.fd_org_type IN (1, 2)
      AND e.fd_is_available = 1
),
ascend AS (
    SELECT 
        DeptId,
        DeptName,
        Path,
        Level,
        CAST(DeptName AS nvarchar(MAX)) AS FullPath
    FROM seed
    WHERE Level = 0
    
    UNION ALL
    
    SELECT 
        s.DeptId,
        s.DeptName,
        s.Path,
        s.Level,
        CAST(a.FullPath + N' / ' + s.DeptName AS nvarchar(MAX))
    FROM seed s
    INNER JOIN ascend a ON s.Path = a.DeptId
    WHERE s.Level = a.Level + 1
),
aff_path AS (
    SELECT 
        DeptId,
        MAX(FullPath) AS Affiliation
    FROM ascend
    GROUP BY DeptId
)
SELECT 
    p.LoginName AS id,
    p.LoginName AS username,
    p.PersonName AS display_name,
    p.Email AS email,
    p.MobileNo AS phone,
    pe.fd_create_time AS created_at,
    pe.fd_alter_time AS updated_at,
    CASE 
        WHEN p.Sex = 'M' THEN 'Male'
        WHEN p.Sex = 'F' THEN 'Female'
        ELSE ''
    END AS gender,
    N'zh' AS language,
    pd.DeptId AS dept_id,
    c.CompanyName AS company_name,
    ISNULL(ap.Affiliation, d.DeptName) AS affiliation,
    CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
    CAST(NULL AS nvarchar(50)) AS type
    -- 注意: 已移除 groups 字段 - 改用 vw_user_group_membership 视图
FROM person p
INNER JOIN dbo.sys_org_element pe ON pe.fd_id = p.PersonId AND pe.fd_org_type = 8
LEFT JOIN person_dept pd ON p.PersonId = pd.PersonId AND pd.RowNum = 1
LEFT JOIN dept_company dc ON pd.DeptId = dc.DeptId
LEFT JOIN dept d ON pd.DeptId = d.DeptId
LEFT JOIN aff_path ap ON pd.DeptId = ap.DeptId
LEFT JOIN company c ON dc.CompanyId = c.CompanyId
WHERE dc.CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581');
GO

PRINT 'vw_casdoor_users_sync 视图优化完成 ✓';
PRINT '';
GO

-- =====================================================
-- 2. 创建用户-组织关联专用视图 (支持多组织归属)
-- =====================================================
PRINT '正在创建 vw_user_group_membership 视图...';
GO

IF OBJECT_ID('dbo.vw_user_group_membership', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_user_group_membership];
GO

CREATE VIEW [dbo].[vw_user_group_membership] AS
WITH elem AS (
    SELECT 
        fd_id AS ElemId,
        fd_name AS ElemName,
        fd_org_type AS OrgType,
        fd_parentid AS ParentId,
        fd_parentorgid AS ParentOrgId
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
),
person AS (
    SELECT 
        e.ElemId AS PersonId,
        p.fd_login_name AS LoginName
    FROM elem e
    INNER JOIN dbo.sys_org_person p ON e.ElemId = p.fd_id
    WHERE e.OrgType = 8  -- 人员类型
      AND p.fd_login_name IS NOT NULL
),
post AS (
    SELECT 
        ElemId AS PostId,
        ParentId,
        ParentOrgId
    FROM elem
    WHERE OrgType = 4  -- 岗位
),
dept AS (
    SELECT 
        ElemId AS DeptId,
        ElemName AS DeptName
    FROM elem
    WHERE OrgType IN (1, 2)  -- 部门和公司
),
-- 通过岗位反查部门，形成用户-部门多对多集合
post_map AS (
    SELECT 
        spp.fd_personid AS PersonId,
        pe.fd_parentorgid AS ParentOrgId,
        pe.fd_parentid AS ParentId
    FROM dbo.sys_org_post_person AS spp
    INNER JOIN dbo.sys_org_element AS pe ON pe.fd_id = spp.fd_postid AND pe.fd_org_type = 4
),
person_dept AS (
    SELECT DISTINCT 
        pm.PersonId,
        COALESCE(d1.DeptId, d2.DeptId) AS DeptId
    FROM post_map pm
    LEFT JOIN dept d1 ON d1.DeptId = pm.ParentOrgId
    LEFT JOIN dept d2 ON d2.DeptId = pm.ParentId
    WHERE COALESCE(d1.DeptId, d2.DeptId) IS NOT NULL
),
company_filter AS (
    -- 递归查找两个目标公司下的所有部门
    SELECT DISTINCT 
        fd_id AS DeptId
    FROM dbo.sys_org_element
    WHERE fd_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
      AND fd_is_available = 1
    
    UNION ALL
    
    SELECT 
        e.fd_id
    FROM dbo.sys_org_element e
    INNER JOIN company_filter cf ON (e.fd_parentid = cf.DeptId OR e.fd_parentorgid = cf.DeptId)
    WHERE e.fd_org_type IN (1, 2)
      AND e.fd_is_available = 1
)
SELECT 
    p.LoginName AS user_id,
    pd.DeptId AS group_id
FROM person p
JOIN person_dept pd ON p.PersonId = pd.PersonId
JOIN company_filter cf ON pd.DeptId = cf.DeptId;
GO

PRINT 'vw_user_group_membership 视图创建完成 ✓';
PRINT '';
GO

-- =====================================================
-- 3. 优化组织结构同步视图 (修复父级组织逻辑)
-- =====================================================
PRINT '正在优化 vw_org_structure_sync 视图...';
GO

ALTER VIEW [dbo].[vw_org_structure_sync] AS 
SELECT
    d.fd_id AS Id,
    d.fd_id AS name,
    d.fd_name AS display_name,
    CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
    
    -- 父级组织: 只有真正有父级时才设置,否则 NULL (顶级组织)
    CASE 
        WHEN d.fd_parentid IS NOT NULL THEN d.fd_parentid
        WHEN d.fd_parentorgid IS NOT NULL THEN d.fd_parentorgid
        ELSE NULL
    END AS parent_group,
    
    'Physical' AS type,
    d.fd_create_time AS created_time,
    d.fd_alter_time AS updated_time,
    COALESCE(c1.fd_name, c2.fd_name, '未知') AS department,
    d.fd_name AS dept_name,
    COALESCE(c1.fd_name, c2.fd_name) AS company_name,
    
    -- parent_id 同样改为 NULL (避免使用字符串 'fzswjtOrganization')
    CASE 
        WHEN d.fd_parentid IS NOT NULL THEN d.fd_parentid
        WHEN d.fd_parentorgid IS NOT NULL THEN d.fd_parentorgid
        ELSE NULL
    END AS parent_id,
    
    COALESCE(c1.fd_id, c2.fd_id) AS company_id,
    CAST(ISNULL(d.fd_is_available, 1) AS bit) AS is_enabled
FROM dbo.sys_org_element AS d
LEFT JOIN dbo.sys_org_element AS c1 ON c1.fd_id = d.fd_parentorgid
LEFT JOIN dbo.sys_org_element AS c2 ON c2.fd_id = d.fd_parentid
WHERE (d.fd_org_type = 2 OR d.fd_org_type = 1)  -- 只查询公司和部门
  AND (COALESCE(c1.fd_id, c2.fd_id) IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'));
GO

PRINT 'vw_org_structure_sync 视图优化完成 ✓';
PRINT '';
GO

-- =====================================================
-- 4. 验证视图数据
-- =====================================================
PRINT '========================================';
PRINT '视图优化完成! 正在验证数据...';
PRINT '========================================';
PRINT '';

-- 验证用户视图
PRINT '1. 用户视图数据统计:';
SELECT 
    COUNT(*) AS total_users,
    COUNT(DISTINCT dept_id) AS unique_depts,
    COUNT(DISTINCT company_name) AS unique_companies
FROM dbo.vw_casdoor_users_sync;
PRINT '';

-- 验证用户-组织关联视图
PRINT '2. 用户-组织关联统计:';
SELECT 
    COUNT(*) AS total_memberships,
    COUNT(DISTINCT username) AS unique_users,
    COUNT(DISTINCT group_id) AS unique_groups
FROM dbo.vw_user_group_membership;
PRINT '';

-- 验证组织视图
PRINT '3. 组织结构统计:';
SELECT 
    COUNT(*) AS total_orgs,
    SUM(CASE WHEN parent_group IS NULL THEN 1 ELSE 0 END) AS top_level_orgs,
    COUNT(DISTINCT company_id) AS unique_companies
FROM dbo.vw_org_structure_sync;
PRINT '';

-- 检查多组织归属情况
PRINT '4. 多组织归属用户统计:';
SELECT 
    COUNT(*) AS users_with_multiple_groups
FROM (
    SELECT username, COUNT(*) AS group_count
    FROM dbo.vw_user_group_membership
    GROUP BY username
    HAVING COUNT(*) > 1
) AS multi_group_users;
PRINT '';

PRINT '========================================';
PRINT '全部优化完成! ✓';
PRINT '========================================';
PRINT '';
PRINT '下一步操作:';
PRINT '1. 更新 .env 文件: EKP_USER_GROUP_VIEW=vw_user_group_membership';
PRINT '2. 重新运行同步: dotnet run';
PRINT '3. 验证 Casdoor 中用户的组织归属是否正确';
GO
