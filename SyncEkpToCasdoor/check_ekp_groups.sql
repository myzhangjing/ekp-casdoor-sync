-- 检查EKP数据库中用户的Groups字段
SELECT TOP 20 
    username,
    display_name,
    Groups,
    LEN(Groups) as groups_length
FROM vw_casdoor_users_sync
WHERE Groups IS NOT NULL AND Groups != ''
ORDER BY updated_time DESC;

-- 统计有Groups数据的用户数量
SELECT 
    COUNT(*) as total_users,
    SUM(CASE WHEN Groups IS NOT NULL AND Groups != '' THEN 1 ELSE 0 END) as users_with_groups
FROM vw_casdoor_users_sync;
