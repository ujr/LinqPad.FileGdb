# SQL in the File GDB

The File Geodatabase supports a subset of SQL.
This is ducumented by Esri in

- the page *File geodatabases SQL reference* in the ArcGS Pro
  documentation at
  <https://pro.arcgis.com/en/pro-app/latest/help/data/geodatabases/manage-file-gdb/sql-reporting-and-anlysis-file-geodatabases.htm>
- the file *FileGDB_SQL.htm* in the FileGDB API documentation

from which the following information has been sourced.

## Preface

**Fields** *may* be enclosed in double quotes. They *must* be
enclosed in double quotes if they collide with keywords or are
otherwise ambiguous.

**Strings** must always be enclosed in single quotes.

**Numbers** always use the point `.` as the decimal delimiter,
regardless of regional settings.

**Dates** are stored in the File Geodatabase relative to 1899-12-30
at 00:00:00 (the epoch). Date literals are written as strings preceded
by the `date` keyword, as in `"DateField" = date '1999-01-12'` or
`"TimeStampField" = date '1999-01-12 08:12:34'`.

**Alias** names (after the `AS` keyword) are supported (see grammar).

Supported DML statements: SELECT, INSERT, UPDATE, DELETE.

Supported DDL statements: CREATE/ALTER/DROP TABLE, CREATE/DROP INDEX.

## Field Types

|SQL Data Type|Corresponding Esri Type|
|-------------|-----------------------|
|INTEGER, INT|Long Integer|
|SMALLINT|Short Integer|
|NUMERIC(precision,scale)|Float or Double (depending on scale)|
|DECIMAL(precision,scale)|FLoat or Double (depending on scale)|
|FLOAT(precision)|Float or Double (depending on scale!?)|
|REAL|Float|
|DOUBLE PRECISION|Double|
|CHAR, CHARACTER|Text|
|VARCHAR|Text|
|DATE|Date|
|TIME|Date|
|TIMESTAMP|Date|
|OBJECTID|Object ID|
|VARBINARY, BINARY|Blob|
|GUID|Guid|
|GLOBALID|Global ID|
|XML|XML|

## Operators

`+` `-` `*` `/` — arithmetics on numeric values

`=` `<` `<=` `<>` `>` `>=` — comparison on numbers, strings, and dates

`e BETWEEN x AND y` — equivalent to `x <= e AND e <= y`

`e NOT BETWEEN x AND y` — equivalent to `NOT x <= e AND e <= y`

`EXISTS (subquery)` — returns TURE iff the subquery yields at least one record

`e IN (value1, ...)` — equivalent to `e = value1 OR e = ...`

`e NOT IN (value1, ...)` — equivalent to `e <> value1 AND e <> ...`

`e [NOT] IN (subquery)` — equality against values from a subquery

`e IS NULL` — true iff the field `e` is NULL

`e IS NOT NULL` — true iff the field `e` is not NULL

`x [NOT] LIKE y [ESCAPE 'character']` — with wildcards `%` and `_`
 but still case sensitive

`AND` `OR` `NOT` — logical connectors

## Functions

### Date Functions

- `CURRENT_DATE`
- `CURRENT_TIME`
- `CURRENT_TIMESTAMP` — date and time
- `EXTRACT(field FROM source)` — *field* is one of `YEAR`, `MONTH`, `DAY`,
  `HOUR`, `MINUTE`, `SECOND`, and *source* is a date-time expression

### String Functions

- `CHAR_LENGTH(s)` — number of characters in *s*
- `CONCAT(s1, s2)` — concatenate the two strings
- `LOWER(s)` — return lower case equivalent of *s*
- `POSITION(s1 IN s2)` — position of *s1* within *s2*
- `SUBSTRING(s FROM start FOR length)` — substring of *s*
- `TRIM(BOTH|LEADING|TRAILING c FROM s)` — character *c* removed from string *s*
- `UPPER(s)` — upper case equivalent of *s*

### Numeric Functions

- `ABS(x)`
- `ACOS(x)` — angle in radians
- `ASIN(x)` — angle in radians
- `ATAN(x)`
- `CEILING(x)`
- `COS(x)`
- `FLOOR(x)`
- `LOG(x)` — natural logarithm
- `LOG10(x)` — base 10 logarithm
- `MOD(x,y)` — remainder of x/y, both arguments integers
- `POWER(x,y)` — *x* (numeric) to the power of *y* (integer)
- `ROUND(x,k)` — round to *k* (integer) decimal places; if *k*
  is negative, round to abs(k) places left of the decimal point
- `SIGN(x)` — -1,0,1 if *x* is negative, zero, positive
- `SIN(x)`
- `TAN(x)`
- `TRUNCATE(x,k)` — truncate to *k* (integer) places right of
  decimal point, or, if *k* is negative, *k* places left of it

### Miscellaneous

- `CAST(e AS type)` — *type* is one of `CHAR`, `VARCHAR`, `INTEGER`,
  `SMALLINT`, `REAL`, `DOUBLE`, `DATE`, `TIME`, `DATETIME`, `NUMERIC`,
  or `DECIMAL`
- `COALESCE(x, ...)` — returns the first non-null argument
- `NULLIF(x,y)` — returns NULL if *x* equals *y*, else *x*

## Aggregate Functions

- `AVG(expr)` — average, ignoring NULL values
- `COUNT(*)` — number of records in the table
- `COUNT(expr)` — number of values, including NULL
- `MAX(expr)` — maximum value in set (NULL is ignored)
- `MIN(expr)` — minimum value in set (NULL is ingored)
- `STDDEV(expr)` — standard deviation of expr in set
- `SUM(expr)` — add values in set (NULL is ignored)
- `VAR(expr)` — variance of expr in set

> File Geodatabase API documentation mentions aggregate functions
> only in the context of sub queries,
> as in `"Pop" > (SELECT MAX("Pop") FROM countries)`

## CASE expressions

As in the following example (taken from Esri's *File geodatabase
SQL reference* page):

```sql
SELECT name, salary,
  CASE
    WHEN salary <= 2000 THEN 'low'
    WHEN salary > 2000 AND salary <= 3000 THEN 'average'
    WHEN salary > 3000 THEN 'high'
  END AS salary_level
  FROM employees
```

> Missing in File Geodatabase API documentation

## Clauses

To restrict, order, or modify the results of a query:

- GROUP BY
- HAVING
- Joins: CROSS JOIN, INNER JOIN, LEFT OUTER JOIN, RIGHT OUTER JOIN
- ORDER BY

> File Geodatabase API documentation only mentions `ORDER BY`

## Syntax

- Braces indicate zero or more repetations
- Brackets indicate optional parts
- Note: grammar is incomplete

### DML statements

```text
SELECT [ALL|DISTINCT] select-list FROM table-list
  [WHERE where-clause]
  [ORDER BY sort-spec {, sort-spec}]

INSERT INTO tablename [(column-name {, column-name})]
  VALUES (value {, value})
INSERT INTO tablename
  VALUES (subquery)

UPDATE tablename
  SET column-name = expression {, column-name = expression}
  [WHERE where-clause]

DELETE FROM tablename
  [WHERE where-clause]

select-list ::= * | select-sublist {, select-sublist}
select-sublist ::= expression [[AS] alias] | table-name.* | correlation-name.*

table-list ::= table {, table}
table ::= tablename [[AS] correlation-name]

where-clause ::= search-term { OR where-clause }
search-term ::= search-factor { AND search-term}
search-factor ::= [NOT] search-primary
search-primary ::= predicate | (where-clause)
predicate ::= column IS [NOT] NULL |
  expression compop expression |
  expression [NOT] BETWEEN expression AND expression |
  expression [NOT] IN (value{, value}) |
  expression [NOT] IN (sub-query)
  expression [NOT] LIKE pattern [ESCAPE 'char'] |
  EXISTS (sub-query)
compop ::= = | < | <= | <> | > | >=
expression ::= term {+|- term}
term ::= factor {*|/ factor}
factor ::= [+|-]primary
primary ::= literal|column|function|(expression)

sort-spec ::= column-name [ASC|DESC]
```

### DDL statements

```text
CREATE TABLE tablename (column-def {, column-def})
ALTER TABLE tablename ADD [COLUMN] column-def
ALTER TABLE tablename DROP [COLUMN] column-name
DROP TABLE tablename

CREATE [UNIQUE] INDEX indexname ON tablename
  (column [ASC|DESC] {, column [ASC|DESC]})

DROP INDEX tablename.indexname

column ::= [qualifier.]column-name
column-def ::= column-name data-type [DEFAULT default-value] [[NOT] NULL]
data-type ::= [VAR]CHAR(length)|
  INT[EGER]|SMALLINT|NUMERIC(precision[,scale])|DECIMAL(precision[,scale])|
  REAL|DOUBLE PRECISION|FLOAT[(precision)]|
  DATE|TIME|TIMESTAMP|[VAR]BINARY
default-value ::= literal | NULL

```
