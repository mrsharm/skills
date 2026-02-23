# Judge Report: Resilience skill should not activate for simple try-catch question

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent provided a well-structured, accurate, and practical answer covering three complementary approaches to error handling in ASP.NET Core: inline status code returns, global exception middleware, and custom exception filters. The code examples are correct and idiomatic. The mention of the [ApiController] attribute's automatic 400 behavior is a useful detail. The only notable omission is ProblemDetails, which is the recommended structured error response format in modern ASP.NET Core. The answer was generated efficiently in a single turn with no tool calls needed, which is appropriate for a knowledge question.

- **Did NOT suggest resilience/Polly patterns for a basic error handling question**: 5/5 — The agent correctly stayed focused on basic error handling patterns (status code helpers, exception middleware, exception filters) and did not introduce Polly or resilience patterns, which would be irrelevant to this question.
- **Suggested exception middleware, ProblemDetails, or try-catch patterns instead**: 4/5 — The agent suggested exception middleware (UseExceptionHandler) and a custom exception filter (IExceptionFilter), both of which are appropriate and practical patterns. It also covered returning proper status codes directly from actions. It did not mention ProblemDetails specifically, which is a modern ASP.NET Core convention for structured error responses and would have been a nice addition, but the overall coverage of error handling patterns is solid and well-structured.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent provided an excellent, focused, and well-structured answer. It included a practical code example with try-catch, a comprehensive table of status code helper methods, global error handling via middleware with ProblemDetails (Results.Problem), and a useful tip about [ApiController] automatic validation. No unnecessary topics like Polly were introduced. The response is accurate, well-organized, and directly addresses the user's question. No tools were needed for this knowledge-based question, so zero tool calls is appropriate.

- **Did NOT suggest resilience/Polly patterns for a basic error handling question**: 5/5 — The agent correctly stayed focused on basic error handling patterns (try-catch, helper methods, middleware) and did not introduce Polly or resilience patterns, which would be off-topic for this question.
- **Suggested exception middleware, ProblemDetails, or try-catch patterns instead**: 5/5 — The agent covered all three relevant patterns: try-catch in controller actions (with a full code example), exception-handling middleware (app.UseExceptionHandler with Results.Problem which uses ProblemDetails), and the built-in ControllerBase helper methods. The response also mentions the [ApiController] attribute's automatic model validation behavior. The ProblemDetails usage is demonstrated via Results.Problem() in the middleware example.

## Baseline Agent Output
```


In ASP.NET Core, use built-in action result helpers and exception handling:

**1. Return proper status codes from actions:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var item = _repo.Find(id);
        if (item == null)
            return NotFound();              // 404
        return Ok(item);                    // 200
    }

    [HttpPost]
    public IActionResult Create(ItemDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);  // 400

        var item = _repo.Create(dto);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item); // 201
    }
}
```

**2. Global exception handling with middleware:**
```csharp
// Program.cs
app.UseExceptionHandler(app => app.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
}));
```

**3. For more control, use a custom exception filter:**
```csharp
public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, message) = context.Exception switch
        {
            KeyNotFoundException   => (404, "Resource not found"),
            UnauthorizedAccessException => (403, "Forbidden"),
            _                     => (500, "Internal server error")
        };

        context.Result = new ObjectResult(new { error = message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

// Register in Program.cs
builder.Services.AddControllers(o => o.Filters.Add<ApiExceptionFilter>());
```

**Key helpers:** `Ok()`, `BadRequest()`, `NotFound()`, `Conflict()`, `StatusCode(int)`, `CreatedAtAction()`. The `[ApiController]` attribute auto-returns `400` for invalid model state.
```

## With-Skill Agent Output
```


In ASP.NET Core controllers, use built-in helper methods to return proper HTTP status codes:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        try
        {
            var order = await _repository.GetByIdAsync(id);

            if (order is null)
                return NotFound(new { message = $"Order {id} not found" }); // 404

            return Ok(order); // 200
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid(); // 403
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An unexpected error occurred" }); // 500
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(OrderDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState); // 400

        var order = await _repository.CreateAsync(dto);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order); // 201
    }
}
```

**Common helper methods** on `ControllerBase`:

| Method | Status Code |
|---|---|
| `Ok()` | 200 |
| `CreatedAtAction()` | 201 |
| `NoContent()` | 204 |
| `BadRequest()` | 400 |
| `Unauthorized()` | 401 |
| `Forbid()` | 403 |
| `NotFound()` | 404 |
| `Conflict()` | 409 |
| `StatusCode(n)` | Any |

**Tip:** For global error handling, use exception-handling middleware instead of try/catch in every action:

```csharp
app.UseExceptionHandler("/error");

app.Map("/error", (HttpContext ctx) =>
{
    return Results.Problem(statusCode: 500, title: "An unexpected error occurred");
});
```

The `[ApiController]` attribute automatically returns `400 Bad Request` for invalid model state, so you often don't need the manual `ModelState.IsValid` check.
```