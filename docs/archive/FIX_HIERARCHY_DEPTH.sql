-- =====================================================
-- 修复 vw_org_structure_sync 视图：支持多层级部门嵌套
-- =====================================================
-- 问题：当前视图只返回公司直属的部门，缺失部门下的子部门
-- 解决：递归获取所有层级的部门

USE [ekp];
GO

PRINT '修复 vw_org_structure_sync 视图 - 支持多层级部门...';
GO

ALTER VIEW [dbo].[vw_org_structure_sync] AS
WITH 
-- 1. 定义目标公司
target_companies AS (
    SELECT fd_id AS company_id
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
      AND fd_org_type = 1  -- 公司
      AND fd_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
),
-- 2. 递归获取目标公司下的所有组织（包括多层部门）
org_hierarchy AS (
    -- 锚点：目标公司本身
    SELECT 
        e.fd_id AS id,
        e.fd_name AS display_name,
        e.fd_org_type AS org_type,
        CAST(NULL AS nvarchar(255)) AS parent_id,
        CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
        e.fd_create_time AS created_time,
        e.fd_alter_time AS updated_time,
        e.fd_is_available AS is_enabled,
        0 AS depth
    FROM dbo.sys_org_element e
    INNER JOIN target_companies tc ON e.fd_id = tc.company_id
    WHERE e.fd_is_available = 1
    
    UNION ALL
    
    -- 递归：获取子部门
    SELECT 
        e.fd_id,
        e.fd_name,
        e.fd_org_type,
        COALESCE(e.fd_parentorgid, e.fd_parentid) AS parent_id,
        CAST(N'fzswjtOrganization' AS nvarchar(100)),
        e.fd_create_time,
        e.fd_alter_time,
        e.fd_is_available,
        oh.depth + 1
    FROM dbo.sys_org_element e
    INNER JOIN org_hierarchy oh ON COALESCE(e.fd_parentorgid, e.fd_parentid) = oh.id
    WHERE e.fd_is_available = 1
      AND e.fd_org_type = 2  -- 部门
      AND oh.depth < 10  -- 防止循环，最多10层
)
SELECT 
    id,
    id AS name,
    display_name,
    parent_id,
    CASE WHEN org_type = 1 THEN 'company' ELSE 'department' END AS type,
    owner,
    created_time,
    updated_time,
    CAST(NULL AS NVARCHAR(255)) AS dept_id,
    CAST(CASE WHEN is_enabled = 1 THEN 1 ELSE 0 END AS BIT) AS is_enabled
FROM org_hierarchy
WHERE is_enabled = 1;
GO

PRINT '✓ vw_org_structure_sync 视图修复完成';
PRINT '  - 新增递归CTE支持多层级部门（最多10层）';
PRINT '  - 公司下可嵌套部门→子部门→小组等多层结构';
PRINT '';
GO

-- 验证修复效果
PRINT '验证组织层级分布:';
GO

WITH depth_calc AS (
    SELECT 
        id,
        display_name,
        parent_id,
        CASE 
            WHEN parent_id IS NULL THEN 0
            WHEN EXISTS (SELECT 1 FROM dbo.vw_org_structure_sync p WHERE p.id = o.parent_id AND p.parent_id IS NULL) THEN 1
            WHEN EXISTS (
                SELECT 1 FROM dbo.vw_org_structure_sync p 
                INNER JOIN dbo.vw_org_structure_sync pp ON p.parent_id = pp.id AND pp.parent_id IS NULL
                WHERE p.id = o.parent_id
            ) THEN 2
            ELSE 3
        END AS depth_level
    FROM dbo.vw_org_structure_sync o
)
SELECT 
    depth_level AS [层级],
    COUNT(*) AS [组织数量],
    STRING_AGG(CAST(display_name AS nvarchar(max)), ', ') WITHIN GROUP (ORDER BY display_name) AS [示例]
FROM depth_calc
GROUP BY depth_level
ORDER BY depth_level;
GO

PRINT '视图修复完成！请重新运行同步程序。';
GO
