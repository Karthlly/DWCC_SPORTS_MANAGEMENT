using DWCCSportsManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DWCCSportsManagement.Controllers
{
    public class ReportController : Controller
    {
        private readonly SportsContext _context;

        public ReportController(SportsContext context)
        {
            _context = context;
        }

       

    }
}
