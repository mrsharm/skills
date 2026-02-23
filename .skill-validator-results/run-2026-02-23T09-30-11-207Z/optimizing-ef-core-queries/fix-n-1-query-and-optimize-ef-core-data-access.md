# Judge Report: Fix N+1 query and optimize EF Core data access

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent gave a clear, well-structured answer that correctly identifies the N+1 problem, explains it well, and provides the optimal fix (Select projection) along with an Include-based alternative. The code examples are correct and idiomatic. However, it missed two relevant optimization techniques: AsNoTracking() for read-only queries and AsSplitQuery() for multiple Includes. These omissions prevent a perfect score, but the core answer is strong and actionable.

- **Identified the N+1 query pattern as the root cause (lazy loading triggering per-row queries)**: 5/5 — The agent clearly and immediately identified the N+1 query problem, explaining that ToListAsync() loads only Orders and each subsequent access to order.Items and order.Customer triggers separate lazy-loading SQL queries.
- **Suggested using Include() for eager loading OR Select() projection to fix N+1**: 5/5 — The agent provided both approaches: the preferred Select() projection as the primary fix, and the Include()-based alternative for cases where full entity objects are needed. Both were shown with correct code examples.
- **Recommended AsNoTracking() since this is a read-only query**: 1/5 — The agent did not mention AsNoTracking() at all. For a read-only query returning DTOs, this is a relevant performance optimization. While the Select() projection implicitly avoids tracking in many cases, explicitly mentioning AsNoTracking() — especially for the Include() alternative — would be important.
- **Mentioned AsSplitQuery() as an option for multiple Includes to avoid cartesian explosion**: 1/5 — The agent did not mention AsSplitQuery() at all. Given the Include() alternative involves multiple Includes (Items and Customer), mentioning split queries to avoid cartesian explosion would have been valuable.
- **Showed the optimized query using projection (Select) which only fetches needed columns**: 5/5 — The agent provided a clean, correct Select() projection that computes Sum, Count, and fetches CustomerName all within a single LINQ expression. It also correctly noted this avoids over-fetching columns.
- **Explained why ToListAsync() before accessing navigation properties causes the problem**: 5/5 — The agent clearly explained that ToListAsync() loads only the Orders table, and then accessing navigation properties (Items, Customer) in the loop triggers separate lazy-loading queries — directly explaining why hundreds of SQL queries appear in the logs.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent's response is excellent. It correctly diagnoses the N+1 problem, provides a clear step-by-step explanation of why 401 queries are generated, offers the optimal fix (projection) with complete code, includes a helpful before/after comparison table, shows the approximate generated SQL, and provides an alternative approach (Include + AsSplitQuery) when full entities are needed. The response is well-structured, accurate, and actionable. The only minor note is that the agent used a 'skill' tool call which seems to be some kind of knowledge retrieval, but the final output quality is top-notch regardless of the path taken.

- **Identified the N+1 query pattern as the root cause (lazy loading triggering per-row queries)**: 5/5 — The agent clearly identified the N+1 query problem, broke it down into the 1 initial query + 200 for Items + 200 for Customer = ~401 queries, and explicitly named lazy loading as the trigger.
- **Suggested using Include() for eager loading OR Select() projection to fix N+1**: 5/5 — The agent suggested both approaches: projection with Select() as the primary fix, and Include() as an alternative when full entities are needed. Both were presented with code examples.
- **Recommended AsNoTracking() since this is a read-only query**: 5/5 — AsNoTracking() is included in both the projection and the Include() code examples, and its benefit ('skips tracking') is explicitly called out in the comparison table.
- **Mentioned AsSplitQuery() as an option for multiple Includes to avoid cartesian explosion**: 5/5 — AsSplitQuery() is explicitly shown in the Include() alternative with a comment explaining it 'avoids cartesian explosion'.
- **Showed the optimized query using projection (Select) which only fetches needed columns**: 5/5 — The agent provided a complete, correct Select() projection that maps directly to OrderDto with only the 4 needed fields. It also showed the approximate generated SQL and explained that aggregation happens in the database.
- **Explained why ToListAsync() before accessing navigation properties causes the problem**: 5/5 — The agent clearly explained that ToListAsync() materializes all orders first (step 1), and then accessing order.Items and order.Customer in the foreach loop triggers lazy-load queries per order (steps 2 and 3). The causal chain is well articulated.

## Baseline Agent Output
```


This is the **N+1 query problem**. `ToListAsync()` loads only the `Orders` table, then each access to `order.Items` and `order.Customer` inside the loop triggers a separate lazy-loading SQL query.

**Fix: Use eager loading with `Include` and project in a single query:**

```csharp
public async Task<List<OrderDto>> GetOrders()
{
    return await _db.Orders
        .Select(order => new OrderDto
        {
            Id = order.Id,
            Total = order.Items.Sum(i => i.Price),
            ItemCount = order.Items.Count,
            CustomerName = order.Customer.Name
        })
        .ToListAsync();
}
```

**Why this works:** EF Core translates the entire `Select` projection into a **single SQL query** with `JOIN`s and `SUM`/`COUNT` computed on the database — instead of 200+ round trips.

**Alternative** (if you need full entity objects, not just a DTO):

```csharp
var orders = await _db.Orders
    .Include(o => o.Items)
    .Include(o => o.Customer)
    .ToListAsync();
```

The `Select` projection approach is preferred here because it also avoids over-fetching columns you don't need.
```

## With-Skill Agent Output
```


This is a classic **N+1 query problem**. Here's what's happening:

1. `ToListAsync()` fires **1 query** to load all 200 orders
2. `order.Items` triggers a lazy-load query **per order** (200 queries)
3. `order.Customer` triggers another lazy-load query **per order** (200 more queries)

**Total: ~401 SQL queries** instead of 1.

## The Fix: Use Projection

Replace the entire method with a single LINQ projection that translates to **one SQL query**:

```csharp
public async Task<List<OrderDto>> GetOrders()
{
    return await _db.Orders
        .AsNoTracking()
        .Select(o => new OrderDto
        {
            Id = o.Id,
            Total = o.Items.Sum(i => i.Price),
            ItemCount = o.Items.Count,
            CustomerName = o.Customer.Name
        })
        .ToListAsync();
}
```

### Why this works

| Before | After |
|--------|-------|
| 401 SQL queries | **1 SQL query** |
| Loads full entity graphs into memory | Only fetches 4 columns |
| Change tracking overhead on all entities | `AsNoTracking()` skips tracking |

EF Core translates the `.Select()` into a single SQL query with `JOIN` + `SUM` + `COUNT` — the aggregation happens in the database, not in C# memory.

### Generated SQL (approximately)

```sql
SELECT o.Id, SUM(i.Price), COUNT(i.Id), c.Name
FROM Orders o
LEFT JOIN OrderItems i ON i.OrderId = o.Id
INNER JOIN Customers c ON o.CustomerId = c.Id
GROUP BY o.Id, c.Name
```

### If you still need full entities elsewhere

Use `.Include()` instead of lazy loading, and prefer `AsSplitQuery()` when including multiple collections:

```csharp
var orders = await _db.Orders
    .AsNoTracking()
    .Include(o => o.Items)
    .Include(o => o.Customer)
    .AsSplitQuery()          // avoids cartesian explosion
    .ToListAsync();
```

But **projection (`.Select()`) is always the best option** when you only need specific fields — it fetches less data and avoids materializing full entities.
```