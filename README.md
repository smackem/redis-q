# redis-q
A REPL to run queries against a Redis database using a language similar to C#'s `from` clause.

![image](doc/screenshot-intro-2.png)

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
redis-q (or more precisly RedisQL, the query language employed by redis-q) uses a slightly extended subset of C#'s LINQ syntax extension:

https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/from-clause

RedisQL adds the following language features not supported by C#:
- Function pipelining
- Ranges
- String literals can be enclosed in either `"` or `'` 
since there is no `char` type
- Dynamic type system

### from and sub-queries
_pending_

### Functions and Pipelining
_pending_

## Data Types
RedisQL is a dynamically-typed language supporting scalar values like integers or strings as well as composite values like lists, enumerables and tuples.

### Scalar types
RedisQL supports the following scalar data types:
| Name | Description | Literal |
| --- | --- | --- |
| int | 64 bit signed integer | `100` |
| real | 64 bit floating point | `12.5` |
| string | unicode string of arbitrary length | `"hello"` or `'world'` |
| bool | boolean value | `true` or `false` |


### Enumerables, List and Ranges
Enumerables in RedisQL are lazily evaluated, whereas lists are discrete collections (as in dotnet `IEnumerable` vs. `IList` or in `Stream` vs. `Collection` in Java).
Enumerables and lists are displayed differently:
`1..3` =>
```
  1
  2
  3
Enumerated 3 element(s)
```
while `[1,2,3];` =>
```
[1, 2, 3]
```
The expression `1..3` denotes a Range, which is a simple enumerable over an *inclusive* range of integers.

The `from` expression usually produces an Enumerable, except when it is nested in another `from` expression. In the latter case, it always evaluated eagerly and produces a List:

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
Tuples are composite values consisting of at least two elements like `(1, "abc")`.
In contrast to lists, tuple elements are called fields and can be named:
```csharp
let user = (name: "bob", role: "admin");
```
redis-q displays collections of uniform tuples in tables:
```csharp
> let users = [(name: "bob", role: "admin"), (name: "alice", role: "guest")];

name   role 
------------
bob    admin
alice  guest
```

### Type Conversion
_pending_

## Bindings
Bind values anytime in the REPL's top most scope using the `let` statement:
```csharp
> let multiplier = 100;
100
> let numbers = [1, 2, 3];
[1, 2, 3]
> let products = from n in numbers select n * multiplier |> collect();
[100, 200, 300]
> let userNames = from k in keys("user-*") select get(k)[".name"] |> collect();    
[alice, bob]
```
The last evaluation's result can be recalled using the identifier `it`:
```csharp
> 1 + 1;
2
> it;
2
> it + 1;
3
```

It's best to bind top-level values to discrete lists instead of enumerations so the value can be iterated multiple times using the `it` identifier. This is why the `collect()` function is used in the preceding samples.

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
