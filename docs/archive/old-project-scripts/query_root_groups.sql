-- 查询两个根节点公司的 parent_id 是否已设为 owner
SELECT 
    name,
    display_name,
    parent_id,
    owner,
    CASE 
        WHEN parent_id = owner THEN '✓ 正确(根节点)'
        ELSE '✗ 错误'
    END AS status
FROM `group`
WHERE owner = 'fzswjtOrganization'
  AND name IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
ORDER BY name;
