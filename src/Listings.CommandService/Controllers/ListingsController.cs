using Listings.CommandService.DTOs;
using Listings.CommandService.Infrastructure;
using Listings.CommandService.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Listings.CommandService.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController(IListingCommandService svc, AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest req, CancellationToken ct)
    {
        try
        {
            var id = await svc.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest req, CancellationToken ct)
    {
        try
        {
            var ok = await svc.UpdateAsync(id, req, ct);
            return ok ? Ok(new { id }) : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await svc.SoftDeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var data = await svc.GetByIdAsync(id, ct);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpGet("/api/outbox/pending")]
    public async Task<IActionResult> OutboxPending(CancellationToken ct)
    {
        var pending = await db.OutboxMessages
            .Where(x => x.PublishedAtUtc == null)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(50)
            .Select(x => new { x.Id, x.Type, x.OccurredAtUtc, x.PublishAttempts })
            .ToListAsync(ct);

        return Ok(pending);
    }
}
