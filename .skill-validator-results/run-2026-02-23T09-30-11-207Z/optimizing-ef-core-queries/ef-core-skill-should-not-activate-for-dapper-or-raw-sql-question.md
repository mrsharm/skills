# Judge Report: EF Core skill should not activate for Dapper or raw SQL question

## Baseline Judge
Overall Score: 5/5
Reasoning: The agent delivered a high-quality, accurate, and comprehensive answer to the Dapper multi-mapping question. It provided correct syntax for Query<Order, Customer, Order> with the map delegate, properly explained splitOn semantics, and went above and beyond by also showing the one-to-many dictionary pattern. The code examples are syntactically correct and follow established Dapper conventions. No tools were needed for this knowledge-based question, so the zero tool calls are appropriate. The response is well-structured with clear explanations and practical code.

- **Did NOT suggest EF Core patterns for a Dapper question**: 5/5 — The agent's response is entirely focused on Dapper. There is no mention of Entity Framework Core, DbContext, Include(), or any other EF Core patterns. The answer stays squarely within the Dapper ecosystem.
- **Provided Dapper-specific guidance using splitOn or multi-mapping**: 5/5 — The response provides excellent Dapper-specific guidance. It demonstrates the Query<TFirst, TSecond, TReturn> multi-mapping API with a clear map function, explains the splitOn parameter and its default behavior, covers both one-to-one (Order→Customer) and one-to-many (Customer→Orders) scenarios with the dictionary deduplication pattern, and includes key points about how splitOn works and when to customize it.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent provided a concise, accurate, and well-structured answer to the Dapper multi-mapping question. The code example is correct and idiomatic C# (using raw string literals, named parameters, proper generics). The key points section adds valuable practical guidance about splitOn defaults, column ordering, and edge cases. No tools were needed for this knowledge-based question, so zero tool calls is appropriate and efficient. The response is focused, complete, and directly addresses the user's question without any extraneous information.

- **Did NOT suggest EF Core patterns for a Dapper question**: 5/5 — The agent's response is entirely focused on Dapper. There is no mention of Entity Framework Core, DbContext, Include(), or any other EF Core patterns. The answer stays squarely within the Dapper ecosystem.
- **Provided Dapper-specific guidance using splitOn or multi-mapping**: 5/5 — The response demonstrates excellent Dapper-specific knowledge. It correctly uses the `Query<TFirst, TSecond, TReturn>` multi-mapping overload, explains the `splitOn` parameter clearly, provides a correct and complete code example with the map function, and includes key points about column ordering, the default splitOn value, and how to handle differently-named primary keys. The code is idiomatic and accurate.

## Baseline Agent Output
```


Here's how to use Dapper's multi-mapping to join Orders with Customers:

```csharp
var sql = @"SELECT o.Id, o.OrderDate, o.Total,
                   c.Id, c.Name, c.Email
            FROM Orders o
            INNER JOIN Customers c ON o.CustomerId = c.Id";

var orders = connection.Query<Order, Customer, Order>(
    sql,
    map: (order, customer) =>
    {
        order.Customer = customer;
        return order;
    },
    splitOn: "Id" // column where Dapper splits between Order and Customer
);
```

**Key points:**

- **`splitOn`** tells Dapper which column marks the boundary between the two objects. It defaults to `Id`. If your Customer PK has a different name (e.g. `CustomerId`), pass `splitOn: "CustomerId"`.
- The generic parameters are `<TFirst, TSecond, TReturn>` — the `map` function receives each mapped object and returns the final result.
- For **one-to-many** (one customer, many orders), use a dictionary to deduplicate:

```csharp
var lookup = new Dictionary<int, Customer>();

connection.Query<Customer, Order, Customer>(
    @"SELECT c.Id, c.Name, c.Email,
             o.Id, o.OrderDate, o.Total
      FROM Customers c
      INNER JOIN Orders o ON o.CustomerId = c.Id",
    map: (customer, order) =>
    {
        if (!lookup.TryGetValue(customer.Id, out var existing))
        {
            existing = customer;
            existing.Orders = new List<Order>();
            lookup[customer.Id] = existing;
        }
        existing.Orders.Add(order);
        return existing;
    },
    splitOn: "Id"
);

var customers = lookup.Values.ToList();
```
```

## With-Skill Agent Output
```


Use Dapper's `Query<TFirst, TSecond, TReturn>` overload with a `splitOn` parameter:

```csharp
using var connection = new SqlConnection(connectionString);

var sql = """
    SELECT o.Id, o.OrderDate, o.Total,
           c.Id, c.Name, c.Email
    FROM Orders o
    INNER JOIN Customers c ON o.CustomerId = c.Id
    """;

var orders = connection.Query<Order, Customer, Order>(
    sql,
    map: (order, customer) =>
    {
        order.Customer = customer;
        return order;
    },
    splitOn: "Id" // column where Dapper splits the row into the second object
).ToList();
```

**Key points:**

- **`splitOn`** tells Dapper which column marks the start of the second object. It defaults to `"Id"`. If your customer PK is named differently (e.g., `CustomerId`), pass `splitOn: "CustomerId"`.
- The **map** function receives one instance of each type per row and returns the final object.
- Column order in your `SELECT` matters — all `Order` columns must come first, then all `Customer` columns.
```