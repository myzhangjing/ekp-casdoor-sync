-- 诊断脚本：查找"张璟"为什么不在视图中
-- 连接：Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;Encrypt=False;

-- 1. 查找所有叫"张璟"或类似名字的人员
PRINT '========== 1. 查找张璟相关人员 =========='
SELECT 
    p.fd_id,
    p.fd_login_name,
    e.fd_name AS display_name,
    e.fd_org_type,
    e.fd_is_available,
    e.fd_parentid,
    e.fd_parentorgid
FROM dbo.sys_org_person p
INNER JOIN dbo.sys_org_element e ON e.fd_id = p.fd_id
WHERE e.fd_name LIKE '%张璟%' 
   OR e.fd_name LIKE '%张菁%'
   OR e.fd_name LIKE '%章璟%'
   OR p.fd_login_name LIKE '%zhangjing%'
   OR p.fd_login_name LIKE '%zhang%jing%';

-- 2. 查找"技术管理部"的组织ID
PRINT ''
PRINT '========== 2. 查找技术管理部 =========='
SELECT 
    fd_id,
    fd_name,
    fd_org_type,
    fd_is_available,
    fd_parentid,
    fd_parentorgid
FROM dbo.sys_org_element
WHERE fd_name LIKE '%技术管理%'
  AND fd_org_type IN (1, 2)
  AND fd_is_available = 1;

-- 3. 查找"福州市城市排水有限公司"的组织ID
PRINT ''
PRINT '========== 3. 查找城市排水公司 =========='
SELECT 
    fd_id,
    fd_name,
    fd_org_type,
    fd_is_available,
    fd_parentid,
    fd_parentorgid
FROM dbo.sys_org_element
WHERE fd_name LIKE '%城市排水%'
  AND fd_org_type = 1
  AND fd_is_available = 1;

-- 4. 查找技术管理部下的所有人员
PRINT ''
PRINT '========== 4. 技术管理部下的人员 =========='
DECLARE @tech_dept_id NVARCHAR(255);
SELECT TOP 1 @tech_dept_id = fd_id 
FROM dbo.sys_org_element 
WHERE fd_name LIKE '%技术管理%' 
  AND fd_org_type = 2 
  AND fd_is_available = 1;

IF @tech_dept_id IS NOT NULL
BEGIN
    PRINT '技术管理部ID: ' + @tech_dept_id;
    
    -- 直接隶属于技术管理部的人员
    SELECT 
        p.fd_login_name,
        e.fd_name AS display_name,
        e.fd_id,
        e.fd_parentid,
        e.fd_parentorgid,
        CASE WHEN p.fd_login_name IS NULL THEN '无登录名' ELSE '有登录名' END AS has_login
    FROM dbo.sys_org_element e
    LEFT JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8
      AND e.fd_is_available = 1
      AND (e.fd_parentid = @tech_dept_id OR e.fd_parentorgid = @tech_dept_id);
    
    -- 通过岗位关系关联到技术管理部的人员
    PRINT ''
    PRINT '通过岗位关系:'
    SELECT DISTINCT
        p.fd_login_name,
        e.fd_name AS display_name,
        e.fd_id,
        post.fd_name AS post_name,
        CASE WHEN p.fd_login_name IS NULL THEN '无登录名' ELSE '有登录名' END AS has_login
    FROM dbo.sys_org_post_person spp
    INNER JOIN dbo.sys_org_element post ON post.fd_id = spp.fd_postid
    INNER JOIN dbo.sys_org_element e ON e.fd_id = spp.fd_personid
    LEFT JOIN dbo.sys_org_person p ON e.fd_id = p.fd_id
    WHERE e.fd_org_type = 8
      AND e.fd_is_available = 1
      AND post.fd_org_type = 4
      AND (post.fd_parentid = @tech_dept_id OR post.fd_parentorgid = @tech_dept_id);
END
ELSE
BEGIN
    PRINT '未找到技术管理部!';
END

-- 5. 验证部门到公司的追溯路径
PRINT ''
PRINT '========== 5. 技术管理部到公司的追溯 =========='
IF @tech_dept_id IS NOT NULL
BEGIN
    ;WITH dept_path AS (
        SELECT 
            fd_id,
            fd_name,
            fd_org_type,
            fd_parentid,
            fd_parentorgid,
            0 AS level,
            CAST(fd_name AS NVARCHAR(MAX)) AS path
        FROM dbo.sys_org_element
        WHERE fd_id = @tech_dept_id
        
        UNION ALL
        
        SELECT 
            e.fd_id,
            e.fd_name,
            e.fd_org_type,
            e.fd_parentid,
            e.fd_parentorgid,
            dp.level + 1,
            CAST(e.fd_name + ' > ' + dp.path AS NVARCHAR(MAX))
        FROM dbo.sys_org_element e
        INNER JOIN dept_path dp ON (e.fd_id = dp.fd_parentid OR e.fd_id = dp.fd_parentorgid)
        WHERE dp.level < 10
          AND e.fd_org_type IN (1, 2)
    )
    SELECT 
        level,
        fd_name,
        fd_org_type,
        fd_id,
        path,
        CASE 
            WHEN fd_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581') 
            THEN '✓ 目标公司' 
            ELSE '' 
        END AS is_target
    FROM dept_path
    ORDER BY level DESC;
END
