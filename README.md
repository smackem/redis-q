# redis-q
A REPL to run queries against a Redis database using a language similar to C#'s LINQ syntax extension.

## Features

Redis-Q
- works with Redis 5 or newer
- supports the basic Redis data types: string, list, hash, set and sorted set
- allows you to write queries in a relational manner to examine the data stored in your Redis instance
- supports well known operations like cross join and sub-queries as well as aggregations like sum, avg, distinct, count, min and max
- is able to extract JSON values using JSON-Path
- works on any desktop platform

## Samples

Assume you create a sample data set in your redis instance consisting of users and sessions, where one user is associated to n sessions:
```redis-cli
set user-1 '{ "name":"bob" }'
set user-2 '{ "name":"alice" }'
hset "session-1" user-key "user-1" "status" "open" "startTime" "2022-04-01 22:03:54"
hset "session-2" user-key "user-1" "status" "closed" "startTime" "2022-03-30 21:03:51"
hset "session-2" user-key "user-1" "status" "closed" "startTime" "2022-03-29 20:14:02"
hset "session-3" user-key "user-1" "status" "closed" "startTime" "2022-03-30 21:03:51"
hset "session-4" user-key "user-2" "status" "closed" "startTime" "2022-03-30 19:22:51"
hset "session-5" user-key "user-2" "status" "open" "startTime" "2022-04-01 19:22:30"
```

```csharp
from userKey in keys("user-*")
from sessionKey in keys("session-*") 
where hget(sessionKey, "user-key") == userKey 
   && hget(sessionKey, "status") == "open"
let sessionStart = hget(sessionKey, "startTime")
select (user: userKey, loggedInSince: sessionStart);
```
Response:
```
  (user-2, 2022-04-01 19:22:30)
  (user-1, 2022-04-01 22:03:54)
```

```csharp
from userKey in keys("user-*")
let openSessionCount = 
    from sessionKey in keys("session-*") 
    where hget(sessionKey, "user-key") == userKey 
    where hget(sessionKey, "status") == "open" 
    select sessionKey 
    |> count()
where openSessionCount > 0
let userJson = get(userKey) 
select (userKey, userJson[".name"]);
```
Response:
```
  (user-2, alice)
  (user-1, bob)
```

## Syntax

### from and sub-queries
_pending_

### Functions and Pipelining
_pending_

## Data Types
### Scalar types
_pending_

### Enumerables, List and Ranges
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

### Tuples
_pending_

### Type Conversion
_pending_

## Built-in Functions
_pending_

## Build and Run

- Install .net SDK 6.0 or higher
- Clone the repository
- From the repo source directory, run  
  `dotnet run --project src/RedisQ.Cli`
- Per default, redis-q connects to Redis at localhost:6379, but you can pass a different connection string when executing redis-q. Run  
  `dotnet run --project src/RedisQ.Cli --help`  
  to see all command-line options.
