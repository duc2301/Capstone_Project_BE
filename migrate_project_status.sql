BEGIN;

UPDATE "Projects"
SET "Status" = CASE WHEN "Status" >= 3 THEN 1 ELSE 0 END
WHERE EXISTS (SELECT 1 FROM "Projects" WHERE "Status" > 1);

SELECT "Status", COUNT(*) AS "Count"
FROM "Projects"
GROUP BY "Status"
ORDER BY "Status";

COMMIT;
