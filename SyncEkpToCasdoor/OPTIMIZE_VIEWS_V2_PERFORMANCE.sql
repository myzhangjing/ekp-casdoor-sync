-- =====================================================
-- EKP 视图深度优化脚本 (性能版)
-- 创建日期: 2025-10-30
-- 目标: 移除递归CTE,简化逻辑,性能提升10倍+
-- =====================================================

USE [ekp];
GO

-- =====================================================
-- 1. 优化用户同步视图 - 移除递归CTE
-- =====================================================
PRINT '正在优化 vw_casdoor_users_sync 视图 (性能版)...';
GO

ALTER VIEW [dbo].[vw_casdoor_users_sync] AS
WITH person_info AS (
    SELECT 
        e.fd_id AS PersonId,
        e.fd_name AS PersonName,
        p.fd_login_name AS LoginName,
        p.fd_email AS Email,
        p.fd_mobile_no AS MobileNo,
        p.fd_sex AS Sex,
        e.fd_create_time AS CreatedTime,
        e.fd_alter_time AS UpdatedTime,
                -- 选择部门ID: 仅返回 org_type=2 的部门
                COALESCE(
                        -- 从岗位的父级选择部门
                        (
                                SELECT TOP 1 CASE 
                                        WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
                                        WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
                                        ELSE NULL END
                                FROM dbo.sys_org_post_person spp
                                INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
                                WHERE spp.fd_personid = e.fd_id 
                                    AND pe.fd_org_type = 4
                                    AND pe.fd_is_available = 1
                        ),
                        -- 回退到人员元素的父级(若为部门)
                        (
                                SELECT TOP 1 CASE 
                                        WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
                                        WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
                                        ELSE NULL END
                        )
                ) AS DeptId
    FROM dbo.sys_org_element e
    INNER JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8 
      AND e.fd_is_available = 1
      AND p.fd_login_name IS NOT NULL
),
-- 简化的公司查找 - 从部门向上最多3层查找公司,无递归
dept_company AS (
    SELECT 
                d.fd_id AS DeptId,
                COALESCE(
                        -- 父级为公司
                        (SELECT TOP 1 p.fd_id FROM dbo.sys_org_element p WHERE p.fd_id = d.fd_parentorgid AND p.fd_org_type = 1 AND p.fd_is_available = 1),
                        (SELECT TOP 1 p.fd_id FROM dbo.sys_org_element p WHERE p.fd_id = d.fd_parentid AND p.fd_org_type = 1 AND p.fd_is_available = 1),
                        -- 父级的父级为公司 (2层)
                        (SELECT TOP 1 gp.fd_id 
                         FROM dbo.sys_org_element pp 
                         INNER JOIN dbo.sys_org_element gp ON (gp.fd_id = pp.fd_parentorgid OR gp.fd_id = pp.fd_parentid)
                         WHERE pp.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                             AND gp.fd_org_type = 1 AND gp.fd_is_available = 1),
                        -- 第三层父级为公司 (3层)
                        (SELECT TOP 1 ggp.fd_id
                         FROM dbo.sys_org_element p1
                         LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
                         LEFT JOIN dbo.sys_org_element ggp ON (ggp.fd_id = p2.fd_parentorgid OR ggp.fd_id = p2.fd_parentid)
                         WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                             AND ggp.fd_org_type = 1 AND ggp.fd_is_available = 1)
                ) AS CompanyId,
                d.fd_name AS DeptName
        FROM dbo.sys_org_element d
        WHERE d.fd_org_type = 2 
            AND d.fd_is_available = 1
)
SELECT 
    p.LoginName AS id,
    p.LoginName AS username,
    p.PersonName AS display_name,
    p.Email AS email,
    p.MobileNo AS phone,
    p.CreatedTime AS created_at,
    p.UpdatedTime AS updated_at,
    CASE p.Sex 
        WHEN 'M' THEN 'Male' 
        WHEN 'F' THEN 'Female' 
        ELSE '' 
    END AS gender,
    N'zh' AS language,
    p.DeptId AS dept_id,
    (SELECT fd_name 
     FROM dbo.sys_org_element 
     WHERE fd_id = dc.CompanyId) AS company_name,
    dc.DeptName AS affiliation,  -- 简化为部门名称
    CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
    NULL AS type
FROM person_info p
LEFT JOIN dept_company dc ON p.DeptId = dc.DeptId
WHERE dc.CompanyId IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581');
GO

PRINT 'vw_casdoor_users_sync 视图优化完成 ✓';
PRINT '  - 移除递归CTE (seed/ascend/aff_path)';
PRINT '  - 简化公司查找逻辑';
PRINT '  - DeptId优先级: 岗位父级 > 元素父组织 > 元素父级';
PRINT '';
GO

-- =====================================================
-- 2. 优化用户-组织关联视图 - 扁平化处理
-- =====================================================
PRINT '正在优化 vw_user_group_membership 视图 (性能版)...';
GO

ALTER VIEW [dbo].[vw_user_group_membership] AS
WITH person AS (
    SELECT 
        e.fd_id AS PersonId, 
        p.fd_login_name AS LoginName
    FROM dbo.sys_org_element e
    INNER JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8 
      AND e.fd_is_available = 1 
      AND p.fd_login_name IS NOT NULL
),
-- 扁平化岗位-部门映射 (仅选择部门org_type=2, 支持一人多岗)
post_dept AS (
    SELECT DISTINCT
        spp.fd_personid AS PersonId,
        CASE 
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentorgid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = pe.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN pe.fd_parentid
            ELSE NULL 
        END AS DeptId
    FROM dbo.sys_org_post_person spp
    INNER JOIN dbo.sys_org_element pe ON pe.fd_id = spp.fd_postid
    WHERE pe.fd_org_type = 4 
      AND pe.fd_is_available = 1
),
-- 回退: 人员直接父级若为部门
person_dept AS (
    SELECT DISTINCT
        e.fd_id AS PersonId,
        CASE 
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentorgid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentorgid
            WHEN EXISTS (SELECT 1 FROM dbo.sys_org_element d WHERE d.fd_id = e.fd_parentid AND d.fd_org_type = 2 AND d.fd_is_available = 1) THEN e.fd_parentid
            ELSE NULL 
        END AS DeptId
    FROM dbo.sys_org_element e
    WHERE e.fd_org_type = 8 AND e.fd_is_available = 1
),
-- 合并映射, 只保留部门
all_person_dept AS (
    SELECT PersonId, DeptId FROM post_dept WHERE DeptId IS NOT NULL
    UNION
    SELECT PersonId, DeptId FROM person_dept WHERE DeptId IS NOT NULL
),
-- 有效部门(属于目标公司,向上最多3层)
valid_depts AS (
    SELECT DISTINCT d.fd_id AS DeptId
    FROM dbo.sys_org_element d
    WHERE d.fd_is_available = 1 
      AND d.fd_org_type = 2
      AND (
            d.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
         OR d.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
         OR EXISTS (
                SELECT 1 
                FROM dbo.sys_org_element p1
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p1.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
                       OR p1.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
          )
         OR EXISTS (
                SELECT 1
                FROM dbo.sys_org_element p1
                LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
                WHERE p1.fd_id IN (d.fd_parentorgid, d.fd_parentid)
                  AND (p2.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
                       OR p2.fd_parentid    IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
          )
      )
)
SELECT DISTINCT
    p.LoginName AS user_id,
    apd.DeptId AS group_id
FROM person p
INNER JOIN all_person_dept apd ON p.PersonId = apd.PersonId
INNER JOIN valid_depts vd ON apd.DeptId = vd.DeptId;
GO

-- =====================================================
-- 3. 扩展组织结构视图: 包含公司与部门,建立父子关系
-- =====================================================
PRINT '正在优化 vw_org_structure_sync 视图 (公司+部门)...';
GO

ALTER VIEW [dbo].[vw_org_structure_sync] AS
WITH org AS (
    SELECT 
        e.fd_id AS group_id,
        e.fd_name AS display_name,
        e.fd_org_type AS org_type,
        CASE WHEN e.fd_org_type = 1 THEN NULL ELSE COALESCE(e.fd_parentorgid, e.fd_parentid) END AS parent_group,
        CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
        e.fd_create_time AS created_time,
        e.fd_alter_time AS updated_time,
        e.fd_is_available AS is_enabled
    FROM dbo.sys_org_element e
    WHERE e.fd_is_available = 1
      AND e.fd_org_type IN (1,2)
),
-- 过滤只保留目标公司及其下属的部门 (向上最多3层验证)
scoped AS (
    SELECT o.*
    FROM org o
    WHERE 
        -- 公司: 直接在目标集合内
        (o.org_type = 1 AND o.group_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
        OR 
        -- 部门: 其祖先包含目标公司
        (o.org_type = 2 AND (
            o.parent_group IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
            OR EXISTS (
                SELECT 1 FROM dbo.sys_org_element p1
                WHERE p1.fd_id = o.parent_group AND (p1.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581') OR p1.fd_parentid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
            )
            OR EXISTS (
                SELECT 1 FROM dbo.sys_org_element p1
                LEFT JOIN dbo.sys_org_element p2 ON (p2.fd_id = p1.fd_parentorgid OR p2.fd_id = p1.fd_parentid)
                WHERE p1.fd_id = o.parent_group AND (p2.fd_parentorgid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581') OR p2.fd_parentid IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581'))
            )
        ))
)
SELECT 
    group_id AS id,
    group_id AS name,
    display_name,
    parent_group AS parent_id,
    CASE WHEN org_type = 1 THEN 'company' ELSE 'department' END AS type,
    owner,
    created_time,
    updated_time,
    CAST(NULL AS NVARCHAR(255)) AS dept_id,
    CAST(CASE WHEN is_enabled = 1 THEN 1 ELSE 0 END AS BIT) AS is_enabled
FROM scoped;
GO

PRINT 'vw_user_group_membership 视图优化完成 ✓';
PRINT '  - 移除递归CTE (company_filter)';
PRINT '  - 扁平化岗位-部门映射';
PRINT '  - 支持真正的一人多岗';
PRINT '';
GO

-- =====================================================
-- 3. 验证优化效果
-- =====================================================
PRINT '========================================';
PRINT '视图优化完成! 正在验证数据...';
PRINT '========================================';
PRINT '';

-- 验证用户视图 (带性能计时)
DECLARE @start DATETIME = GETDATE();
PRINT '1. 用户视图数据统计:';
SELECT 
    COUNT(*) AS total_users,
    COUNT(DISTINCT dept_id) AS unique_depts,
    COUNT(DISTINCT company_name) AS unique_companies,
    COUNT(CASE WHEN email IS NOT NULL AND email <> '' THEN 1 END) AS users_with_email,
    COUNT(CASE WHEN phone IS NOT NULL AND phone <> '' THEN 1 END) AS users_with_phone
FROM dbo.vw_casdoor_users_sync;
PRINT '  查询耗时: ' + CAST(DATEDIFF(ms, @start, GETDATE()) AS nvarchar) + ' 毫秒';
PRINT '';

-- 验证用户-组织关联视图
SET @start = GETDATE();
PRINT '2. 用户-组织关联统计:';
SELECT 
    COUNT(*) AS total_memberships,
    COUNT(DISTINCT user_id) AS unique_users,
    COUNT(DISTINCT group_id) AS unique_groups
FROM dbo.vw_user_group_membership;
PRINT '  查询耗时: ' + CAST(DATEDIFF(ms, @start, GETDATE()) AS nvarchar) + ' 毫秒';
PRINT '';

-- 检查多组织归属情况
PRINT '3. 多组织归属用户统计:';
SELECT 
    COUNT(*) AS users_with_multiple_groups,
    MAX(group_count) AS max_groups_per_user
FROM (
    SELECT user_id, COUNT(*) AS group_count
    FROM dbo.vw_user_group_membership
    GROUP BY user_id
    HAVING COUNT(*) > 1
) AS multi_group_users;
PRINT '';

-- 检查数据质量
PRINT '4. 数据质量检查:';
SELECT 
    COUNT(CASE WHEN dept_id IS NULL THEN 1 END) AS users_without_dept,
    COUNT(CASE WHEN company_name IS NULL THEN 1 END) AS users_without_company,
    COUNT(CASE WHEN created_at IS NULL THEN 1 END) AS users_without_created_time
FROM dbo.vw_casdoor_users_sync;
PRINT '';

PRINT '========================================';
PRINT '全部优化完成! ✓';
PRINT '========================================';
PRINT '';
PRINT '性能提升预期:';
PRINT '  - 用户视图: 从 120秒 降至 < 10秒';
PRINT '  - 成员关系视图: 从 30秒 降至 < 5秒';
PRINT '';
PRINT '下一步操作:';
PRINT '  1. 对比优化前后的查询时间';
PRINT '  2. 运行同步测试: dotnet run';
PRINT '  3. 验证 Casdoor 中用户数据完整性';
GO
