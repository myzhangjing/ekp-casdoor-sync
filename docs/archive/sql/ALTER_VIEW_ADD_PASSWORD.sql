-- 更新视图以包含 MD5 密码字段
-- 注意：由于没有 sqlcmd，需要手动执行或通过程序执行

USE [ekp];
GO

-- 删除并重新创建视图以添加 password_md5 字段
IF OBJECT_ID('vw_casdoor_users_sync', 'V') IS NOT NULL
    DROP VIEW vw_casdoor_users_sync;
GO

CREATE VIEW vw_casdoor_users_sync AS
SELECT 
    p.fd_id AS id,
    p.fd_login_name AS username,
    p.fd_name AS display_name,
    p.fd_email AS email,
    p.fd_mobile_no AS phone,
    p.fd_create_time AS created_at,
    p.fd_alter_time AS updated_at,
    CASE 
        WHEN p.fd_sex = 'M' THEN 'Male'
        WHEN p.fd_sex = 'F' THEN 'Female'
        ELSE NULL
    END AS gender,
    p.fd_lang AS language,
    dept.fd_id AS dept_id,
    comp.fd_name AS company_name,
    dept.fd_name AS affiliation,
    'fzswjtOrganization' AS owner,
    CASE 
        WHEN p.fd_is_admin = 1 THEN 'admin'
        ELSE 'normal-user'
    END AS type,
    p.fd_password AS password_md5  -- 新增：MD5 密码字段
FROM 
    sys_org_person p
    LEFT JOIN sys_org_element dept ON p.fd_parentid = dept.fd_id AND dept.fd_org_type = 2
    LEFT JOIN sys_org_element comp ON dept.fd_parentid = comp.fd_id AND comp.fd_org_type = 1
WHERE 
    p.fd_is_available = 1;
GO

-- 授权查询视图
GRANT SELECT ON vw_casdoor_users_sync TO xxzx;
GO
