using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using System.Security.Claims;

namespace TradingLimitMVC.Controllers
{
    [Authorize]
    [Route("GroupSetting")]
    public class GroupSettingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GroupSettingController> _logger;

        public GroupSettingController(ApplicationDbContext context, ILogger<GroupSettingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: GroupSetting
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(int? groupId, int? typeId)
        {
            try
            {
                var query = _context.GroupSettings.AsQueryable();

                // Apply filters if provided
                if (groupId.HasValue)
                {
                    query = query.Where(g => g.GroupID == groupId.Value);
                }

                if (typeId.HasValue)
                {
                    query = query.Where(g => g.TypeID == typeId.Value);
                }

                var groupSettings = await query.OrderBy(g => g.GroupID)
                                              .ThenBy(g => g.TypeID)
                                              .ThenBy(g => g.Username)
                                              .ToListAsync();

                ViewBag.GroupId = groupId;
                ViewBag.TypeId = typeId;
                
                return View(groupSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group settings");
                TempData["ErrorMessage"] = "An error occurred while loading the group settings.";
                return View(new List<GroupSetting>());
            }
        }

        // GET: GroupSetting/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var groupSetting = await _context.GroupSettings.FirstOrDefaultAsync(m => m.Id == id);
                
                if (groupSetting == null)
                {
                    TempData["ErrorMessage"] = "Group setting not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(groupSetting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group setting with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the group setting.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: GroupSetting/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var model = new GroupSetting();
            return View(model);
        }

        // POST: GroupSetting/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RequestId,GroupID,TypeID,Username,Email")] GroupSetting groupSetting)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate entries
                    var existingEntry = await _context.GroupSettings
                        .FirstOrDefaultAsync(g => g.GroupID == groupSetting.GroupID && 
                                           g.TypeID == groupSetting.TypeID && 
                                           g.Email == groupSetting.Email);
                    
                    if (existingEntry != null)
                    {
                        ModelState.AddModelError("Email", "This combination of Group, Type, and Email already exists.");
                        return View(groupSetting);
                    }

                    groupSetting.CreatedBy = GetCurrentUserName();
                    groupSetting.CreatedDate = DateTime.Now;
                    
                    _context.Add(groupSetting);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Group setting created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                
                return View(groupSetting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group setting");
                TempData["ErrorMessage"] = "An error occurred while creating the group setting.";
                return View(groupSetting);
            }
        }

        // GET: GroupSetting/Edit/5
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var groupSetting = await _context.GroupSettings.FindAsync(id);
                
                if (groupSetting == null)
                {
                    TempData["ErrorMessage"] = "Group setting not found.";
                    return RedirectToAction(nameof(Index));
                }
                
                return View(groupSetting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group setting for edit with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the group setting for editing.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: GroupSetting/Edit/5
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RequestId,GroupID,TypeID,Username,Email,CreatedDate,CreatedBy")] GroupSetting groupSetting)
        {
            if (id != groupSetting.Id)
            {
                TempData["ErrorMessage"] = "Invalid group setting ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate entries (excluding current record)
                    var existingEntry = await _context.GroupSettings
                        .FirstOrDefaultAsync(g => g.Id != id && 
                                           g.GroupID == groupSetting.GroupID && 
                                           g.TypeID == groupSetting.TypeID && 
                                           g.Email == groupSetting.Email);
                    
                    if (existingEntry != null)
                    {
                        ModelState.AddModelError("Email", "This combination of Group, Type, and Email already exists.");
                        return View(groupSetting);
                    }

                    groupSetting.ModifiedBy = GetCurrentUserName();
                    groupSetting.ModifiedDate = DateTime.Now;
                    
                    _context.Update(groupSetting);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Group setting updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                
                return View(groupSetting);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GroupSettingExists(groupSetting.Id))
                {
                    TempData["ErrorMessage"] = "Group setting no longer exists.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group setting with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the group setting.";
                return View(groupSetting);
            }
        }

        // GET: GroupSetting/Delete/5
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var groupSetting = await _context.GroupSettings.FirstOrDefaultAsync(m => m.Id == id);
                
                if (groupSetting == null)
                {
                    TempData["ErrorMessage"] = "Group setting not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(groupSetting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group setting for delete with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the group setting for deletion.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: GroupSetting/Delete/5
        [HttpPost("Delete/{id}"), ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var groupSetting = await _context.GroupSettings.FindAsync(id);
                
                if (groupSetting != null)
                {
                    _context.GroupSettings.Remove(groupSetting);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Group setting deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Group setting not found.";
                }
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group setting with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the group setting.";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool GroupSettingExists(int id)
        {
            return _context.GroupSettings.Any(e => e.Id == id);
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.Identity?.Name ?? 
                   "System";
        }
    }
}