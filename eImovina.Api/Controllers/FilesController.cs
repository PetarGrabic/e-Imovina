using eImovina.Api.Data;
using eImovina.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

/// <summary>
/// Addresses a single equipment file directly (no equipment id in the URL) - separate from
/// EquipmentController, which owns the equipment-scoped list/upload/set-cover actions.
/// </summary>
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public FilesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Plain [Authorize] (any authenticated user), same rationale as
    // EquipmentController.GetEquipmentFiles - read access is broader than write.
    // Never sets Content-Disposition: attachment (the 2-argument PhysicalFile overload) - that
    // would force a download even for gallery <img> thumbnails; Content-Type alone lets the
    // browser render images inline and open PDFs in its own viewer.
    [HttpGet("{fileId:int}")]
    [Authorize]
    public async Task<IActionResult> GetFile(int fileId, CancellationToken ct)
    {
        var file = await _db.EquipmentFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fileId, ct);
        if (file is null)
        {
            return NotFound();
        }

        var fullPath = UploadPaths.FullPath(_env, file.RelativePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, file.ContentType);
    }

    [HttpDelete("{fileId:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> DeleteFile(int fileId, CancellationToken ct)
    {
        var file = await _db.EquipmentFiles.SingleOrDefaultAsync(f => f.Id == fileId, ct);
        if (file is null)
        {
            return NotFound();
        }

        // Physical file goes first. A missing file counts as success (nothing left to clean up),
        // but a genuine I/O failure aborts WITHOUT touching the DB row: an orphan file with
        // correct metadata still pointing at it is recoverable later; a DB row pointing at nothing
        // is not. Only after the physical delete succeeds (or the file was already gone) does the
        // row get removed.
        var fullPath = UploadPaths.FullPath(_env, file.RelativePath);
        try
        {
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch (IOException ex)
        {
            return Problem(detail: $"Datoteku nije moguće izbrisati s diska: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }

        _db.EquipmentFiles.Remove(file);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
