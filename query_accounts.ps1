$env:PGPASSWORD='12345'
$psql = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
$query = @'
SELECT "ProjectName", "ProjectCode", "Id" FROM "Projects" ORDER BY "ProjectName" LIMIT 20
'@
$result = $query | & $psql -U postgres -d CapstoneProjectDb -t -A
Write-Output "Projects:"
Write-Output $result

$query2 = @'
SELECT column_name FROM information_schema.columns WHERE table_name = 'GroupMembers' ORDER BY ordinal_position
'@
$result2 = $query2 | & $psql -U postgres -d CapstoneProjectDb -t -A
Write-Output ""
Write-Output "GroupMembers columns:"
Write-Output $result2
