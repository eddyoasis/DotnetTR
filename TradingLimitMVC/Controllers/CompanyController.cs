using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TradingLimitMVC.Controllers
{
    public class CompanyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CompanyController> _logger;
        public CompanyController(ApplicationDbContext context, ILogger<CompanyController> logger)
        {
            _context = context;
            _logger = logger;
        }
        // GET: Company
        public async Task<IActionResult> Index()
        {
            var companies = await _context.Companies
                .OrderBy(c => c.CompanyName)
                .ToListAsync();
            return View(companies);
        }
        // GET: Company/Create
        public IActionResult Create()
        {
            return View();
        }
        // POST: Company/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Company company)
        {
            if (ModelState.IsValid)
            {
                company.CreatedDate = DateTime.Now;
                _context.Add(company);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Company '{company.CompanyName}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(company);
        }
        // GET: Company/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();
            return View(company);
        }
        // POST: Company/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Company company)
        {
            if (id != company.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    company.UpdatedDate = DateTime.Now;
                    _context.Update(company);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Company '{company.CompanyName}' updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CompanyExists(company.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(company);
        }
        private bool CompanyExists(int id)
        {
            return _context.Companies.Any(e => e.Id == id);
        }
    }
}
