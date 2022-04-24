# redis-q
A debug shell to run queries against a redis database with a language similar to C#'s LINQ syntax extension.

```csharp
from x in [1, 2, 3]
from y in [10, 100, 1000] 
select x * y;
```
yields the result:
```
  10
  100
  1000
  20
  200
  2000
  30
  300
  3000
```
whereas
```csharp
from x in [1, 2, 3]
let m =
    from y in [10, 100, 1000]
    select x * y
select m;
```
yields
```
  [10, 100, 1000]
  [20, 200, 2000]
  [30, 300, 3000]
```
