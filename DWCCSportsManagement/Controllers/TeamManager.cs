using DWCCSportsManagement.Data;
using DWCCSportsManagement.Models;
using ExcelDataReader;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;


namespace DWCCSportsManagement.Controllers
{
    [Authorize(Roles = "Manager")]
    public class TeamManager : Controller
    {
        private readonly SportsContext _context;

        public TeamManager(SportsContext context)
        {
            _context = context;
        }

        // =============================
        // Manage Team
        // =============================
        public async Task<IActionResult> ManageTeam()
        {
            // Load teams with related Coach and Sport
            var teams = await _context.Teams
                .Include(t => t.Coach)
                .Include(t => t.Sport)
                .ThenInclude(s => s.Category)
                .Include(t => t.Sport)
                .ThenInclude(s => s.SubCategory)
                .ToListAsync();

            // Pass sports, categories, subcategories, and coaches to the view
            ViewBag.Sports = await _context.Sports
                .Include(s => s.Category)
                .Include(s => s.SubCategory)
                .ToListAsync();

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SubCategories = await _context.Set<SubCategory>().ToListAsync(); // or _context.SubCategories if you have a DbSet
            ViewBag.Coaches = await _context.Coaches.ToListAsync();

            return View(teams);
        }




        [HttpPost]
        public IActionResult AddTeam(TeamModel newTeam)
        {
            if (!ModelState.IsValid)
            {
                // Debug output
                System.Diagnostics.Debug.WriteLine("ModelState is INVALID!");
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"{kvp.Key} - {error.ErrorMessage}");
                    }
                }

                // Re-populate ViewBag for dropdowns
                ViewBag.Sports = _context.Sports.ToList();
                ViewBag.Coaches = _context.Coaches.ToList();

                return View("ManageTeam", _context.Teams.Include(t => t.Sport).Include(t => t.Coach).ToList());
            }

            // Add new team
            _context.Teams.Add(newTeam);
            _context.SaveChanges();


            TempData["SuccessMessage"] = "Team added successfully!";
            return RedirectToAction("ManageTeam");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.TeamId == id);

            if (team == null)
            {
                TempData["Error"] = "Team not found.";
                return RedirectToAction("ManageTeam");
            }

            // ❌ Check athletes using DB query (BEST)
            bool hasAthletes = await _context.Athletes.AnyAsync(a => a.TeamId == id);

            if (hasAthletes)
            {
                TempData["Error"] = "Cannot delete this team because it has registered athletes.";
                return RedirectToAction("ManageTeam");
            }

            // ❌ Check competition participation
            bool hasCompetitionEntries = await _context.CompetitionParticipants
                .AnyAsync(cp => cp.TeamId == id);

            if (hasCompetitionEntries)
            {
                TempData["Error"] = "Cannot delete this team because it is participating in a competition.";
                return RedirectToAction("ManageTeam");
            }

            // ✔ Safe delete
            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Team deleted successfully!";
            return RedirectToAction("ManageTeam");
        }


        // GET: ManageAthlete
        public IActionResult ManageAthlete()
        {
            var athletes = _context.Athletes
                .Include(a => a.Team)
                .Include(a => a.Sport)
                .ToList();

            // Pass teams to ViewBag
            ViewBag.Teams = _context.Teams.ToList();

            return View(athletes);
        }

        [HttpPost]
        public IActionResult AssignTeam(int athleteId, int teamId)
        {
            var athlete = _context.Athletes.Find(athleteId);
            if (athlete != null)
            {
                athlete.TeamId = teamId;
                _context.SaveChanges();
                TempData["Success"] = "Athlete assigned to team successfully!";
            }
            return RedirectToAction("ManageAthlete");
        }

        public IActionResult GetAttendance(int athleteId)
        {
            var attendance = _context.Attendance
                .Where(a => a.AthleteId == athleteId)
                .Select(a => new
                {
                    a.AttendanceId,
                    a.SessionId,
                    Status = a.Status.ToString(),   // 🔥 ALWAYS output string!
                    SessionName = a.Session.Description
                })
                .ToList();

            return Json(attendance);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            // Sign out the user
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirect to login page or home page
            return RedirectToAction("Index", "Home");
        }



        // POST: Add Athlete
        [HttpPost]
        public async Task<IActionResult> AddAthlete(
     AthleteModel athlete,
     IFormFile MedicalFile,
     IFormFile GradesFile)
        {
            try
            {
                Debug.WriteLine("=== AddAthlete Called ===");

                // Log incoming athlete data
                Debug.WriteLine($"FirstName: {athlete.FirstName}");
                Debug.WriteLine($"LastName: {athlete.LastName}");
                Debug.WriteLine($"Gender: {athlete.Gender}");
                Debug.WriteLine($"Birthdate: {athlete.Birthdate}");
                Debug.WriteLine($"TeamId: {athlete.TeamId}");

                // Log ModelState errors if any
                if (!ModelState.IsValid)
                {
                    Debug.WriteLine("ModelState is INVALID!");
                    foreach (var key in ModelState.Keys)
                    {
                        var errors = ModelState[key].Errors;
                        foreach (var error in errors)
                        {
                            Debug.WriteLine($"ModelState Error - {key}: {error.ErrorMessage}");
                        }
                    }

                    ViewBag.Teams = _context.Teams.Include(t => t.Sport).ToList();
                    return View("ManageAthlete", _context.Athletes.Include(a => a.Team).ToList());
                }

                // Ensure images folder exists
                var imagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                if (!Directory.Exists(imagesFolder))
                {
                    Debug.WriteLine("Creating images folder at: " + imagesFolder);
                    Directory.CreateDirectory(imagesFolder);
                }

                // Save Medical File
                if (MedicalFile != null && MedicalFile.Length > 0)
                {
                    var medicalFileName = Guid.NewGuid() + Path.GetExtension(MedicalFile.FileName);
                    var medicalPath = Path.Combine(imagesFolder, medicalFileName);
                    Debug.WriteLine("Saving MedicalFile to: " + medicalPath);

                    using var medicalStream = new FileStream(medicalPath, FileMode.Create);
                    await MedicalFile.CopyToAsync(medicalStream);

                    athlete.MedicalInfo = "/images/" + medicalFileName;
                }
                else
                {
                    Debug.WriteLine("No MedicalFile uploaded");
                }

                // Save Grades File
                if (GradesFile != null && GradesFile.Length > 0)
                {
                    var gradesFileName = Guid.NewGuid() + Path.GetExtension(GradesFile.FileName);
                    var gradesPath = Path.Combine(imagesFolder, gradesFileName);
                    Debug.WriteLine("Saving GradesFile to: " + gradesPath);

                    using var gradesStream = new FileStream(gradesPath, FileMode.Create);
                    await GradesFile.CopyToAsync(gradesStream);

                    athlete.Grades = "/images/" + gradesFileName;
                }
                else
                {
                    Debug.WriteLine("No GradesFile uploaded");
                }

                // Assign SportId based on Team
                if (athlete.TeamId.HasValue)
                {
                    var team = _context.Teams.FirstOrDefault(t => t.TeamId == athlete.TeamId);
                    if (team != null)
                    {
                        athlete.SportId = team.SportId;
                        Debug.WriteLine($"Assigned SportId {athlete.SportId} based on TeamId {athlete.TeamId}");
                    }
                    else
                    {
                        Debug.WriteLine($"Team not found for TeamId {athlete.TeamId}");
                    }
                }
                else
                {
                    Debug.WriteLine("TeamId is null");
                }

                // Add athlete to DB
                _context.Athletes.Add(athlete);
                var result = await _context.SaveChangesAsync();
                Debug.WriteLine($"SaveChangesAsync result: {result}");

                TempData["Success"] = $"Athlete {athlete.FirstName} {athlete.LastName} added successfully!";
                return RedirectToAction("ManageAthlete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AddAthlete Exception: " + ex.ToString());
                ModelState.AddModelError("", "Failed to add athlete: " + ex.Message);

                ViewBag.Teams = _context.Teams.Include(t => t.Sport).ToList();
                return View("ManageAthlete", _context.Athletes.Include(a => a.Team).ToList());
            }
        }


        
        public IActionResult CompetitionParticipation()
        {
            var competitions = _context.Competitions.ToList();
            var allTeams = _context.Teams.Where(t => t.Status == "Accepted").ToList();
            var participants = _context.CompetitionParticipants.ToList();

            // Prepare available teams per competition
            var availableTeamsByCompetition = new Dictionary<int, List<TeamModel>>();
            foreach (var comp in competitions)
            {
                var participatedTeamIds = participants
                    .Where(p => p.CompetitionId == comp.CompetitionId)
                    .Select(p => p.TeamId)
                    .ToList();

                availableTeamsByCompetition[comp.CompetitionId] = allTeams
                    .Where(t => !participatedTeamIds.Contains(t.TeamId))
                    .ToList();
            }

            ViewBag.AvailableTeamsByCompetition = availableTeamsByCompetition;

            var viewModel = new CompetitionParticipationViewModel
            {
                Competitions = competitions,
                Teams = allTeams,
                Participants = participants
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult UpdateResult(int participantId, string results)
        {
            var participant = _context.CompetitionParticipants
                .FirstOrDefault(p => p.ParticipantId == participantId);

            if (participant != null)
            {
                participant.Results = results;
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Status updated successfully!";
            }

            return RedirectToAction("CompetitionParticipation");
        }

        [HttpPost]
        public IActionResult ParticipateInCompetition(int competitionId, int teamId)
        {
            // Prevent duplicate participation
            if (!_context.CompetitionParticipants.Any(p => p.CompetitionId == competitionId && p.TeamId == teamId))
            {
                var participant = new CompetitionParticipant
                {
                    CompetitionId = competitionId,
                    TeamId = teamId,
                    Results = "N/A"
                };
                _context.CompetitionParticipants.Add(participant);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Team successfully added to the competition!";
            }

            return RedirectToAction("CompetitionParticipation");
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRemarks()
        {
            var remarks = await _context.Performance
                .Join(_context.Sessions,
                      p => p.SessionId,
                      s => s.SessionId,
                      (p, s) => new { p, s })
                .Join(_context.Athletes,
                      ps => ps.p.AthleteId,
                      a => a.AthleteId,
                      (ps, a) => new
                      {
                          ps.p.AthleteId,
                          athleteName = a.FirstName + " " + a.LastName, // get athlete name from Athletes table
                          sessionName = ps.s.Description,
                          ps.p.Remarks,
                          ps.p.Rating,
                          ps.p.MaxRating
                      })
                .ToListAsync();

            return Json(remarks);
        }


        [HttpGet]
        public async Task<IActionResult> GetAllAttendance()
        {
            var attendance = await _context.Attendance
     .Join(_context.Sessions,
           a => a.SessionId,
           s => s.SessionId,
           (a, s) => new { a, s })
     .Join(_context.Athletes,
           as_ => as_.a.AthleteId,
           ath => ath.AthleteId,
           (as_, ath) => new
           {
               as_.a.AthleteId,
               AthleteName = ath.FirstName + " " + ath.LastName,
               SessionName = as_.s.Description,
               Status = as_.a.Status.ToString() // <- convert to string here
           })
     .OrderBy(x => x.AthleteName)
     .ThenBy(x => x.SessionName)
     .ToListAsync();


            return Json(attendance);
        }

        [HttpGet]
        public async Task<IActionResult> ExportAllData()
        {
            // Fetch Performance / Remarks
            var remarks = await _context.Performance
                .Join(_context.Sessions,
                      p => p.SessionId,
                      s => s.SessionId,
                      (p, s) => new { p, s })
                .Join(_context.Athletes,
                      ps => ps.p.AthleteId,
                      a => a.AthleteId,
                      (ps, a) => new
                      {
                          ps.p.AthleteId,
                          AthleteName = a.FirstName + " " + a.LastName,
                          SessionName = ps.s.Description,
                          ps.p.Remarks,
                          ps.p.Rating,
                          ps.p.MaxRating
                      })
                .OrderBy(x => x.AthleteName)
                .ThenBy(x => x.SessionName)
                .ToListAsync();

            // Fetch Attendance
            var attendance = await _context.Attendance
                .Join(_context.Sessions,
                      a => a.SessionId,
                      s => s.SessionId,
                      (a, s) => new { a, s })
                .Join(_context.Athletes,
                      as_ => as_.a.AthleteId,
                      ath => ath.AthleteId,
                      (as_, ath) => new
                      {
                          as_.a.AthleteId,
                          AthleteName = ath.FirstName + " " + ath.LastName,
                          SessionName = as_.s.Description,
                          Status = as_.a.Status
                      })
                .OrderBy(x => x.AthleteName)
                .ThenBy(x => x.SessionName)
                .ToListAsync();

            return Json(new { remarks, attendance });
        }

        [HttpGet]
        public async Task<IActionResult> GetRemarks(int athleteId)
        {
            // Get performance records for this athlete, joining with sessions to get description
            var remarks = await _context.Performance
                .Where(p => p.AthleteId == athleteId)
                .Join(_context.Sessions,
                      p => p.SessionId,
                      s => s.SessionId,
                      (p, s) => new
                      {
                          SessionDescription = s.Description,
                          Remarks = p.Remarks,
                          Rating = p.Rating,
                          MaxRating = p.MaxRating // <-- add this
                      })
                .OrderBy(x => x.SessionDescription)
                .ToListAsync();

            // Get athlete full name
            var athlete = await _context.Athletes
                .Where(a => a.AthleteId == athleteId)
                .Select(a => a.FirstName + " " + a.LastName)
                .FirstOrDefaultAsync();

            // Return JSON with athlete name and session description
            var result = remarks.Select(r => new
            {
                athleteName = athlete,
                sessionName = r.SessionDescription,
                remarks = r.Remarks,
                rating = r.Rating,
                maxRating = r.MaxRating // <-- include for JS
            });

            return Json(result);
        }


        [HttpPost]
        public IActionResult UpdateCompetition(Competition model)
        {
            if (ModelState.IsValid)
            {
                var competition = _context.Competitions.FirstOrDefault(c => c.CompetitionId == model.CompetitionId);
                if (competition != null)
                {
                    competition.Name = model.Name;
                    competition.Type = model.Type;
                    competition.Venue = model.Venue;
                    competition.StartDate = model.StartDate;
                    competition.EndDate = model.EndDate;

                    _context.SaveChanges();
                    TempData["SuccessMessage"] = "Competition updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Competition not found!";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid data submitted!";
            }

            return RedirectToAction("ManageCompetition"); // redirect back to your competitions page
        }


        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int ParticipantId, string Status)
        {
            if (string.IsNullOrEmpty(Status))
            {
                TempData["Error"] = "Please select a valid status.";
                return RedirectToAction("CompetitionParticipants");
            }

            var participant = await _context.CompetitionParticipants
                                            .FirstOrDefaultAsync(p => p.ParticipantId == ParticipantId);
            if (participant == null)
            {
                TempData["Error"] = "Participant not found.";
                return RedirectToAction("CompetitionParticipants");
            }

            participant.Status = Status;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Participant status updated successfully.";
            return RedirectToAction("CompetitionParticipation");
        }

        [HttpGet("TeamManager/GetParticipantsByCompetition/{competitionId}")]
        public IActionResult GetParticipantsByCompetition(int competitionId)
        {
            var participants = _context.CompetitionParticipants
    .Where(p => p.CompetitionId == competitionId)
    .Join(
        _context.Teams,
        cp => cp.TeamId,
        t => t.TeamId,
        (cp, t) => new
        {
            cp.ParticipantId,
            cp.CompetitionId,
            cp.TeamId,
            TeamName = t.TeamName,
            Category = t.Sport != null ? t.Sport.Category.Name : "",
            SubCategory = t.Sport != null ? t.Sport.SubCategory.Name : "",
            cp.Results,
            cp.Status
        }
    ).ToList();


            return Json(participants);
        }



        [HttpGet]
        public IActionResult ManageCompetition()
        {
            var competitions = _context.Competitions.ToList();
            return View(competitions);
        }

        // ✅ Add new competition to the database
        [HttpPost]
        [HttpPost]
        [HttpPost]
        [HttpPost]
        public IActionResult AddCompetition(Competition newComp)
        {
            // Detect time conflict
            bool conflict = _context.Competitions.Any(c =>
                newComp.StartDate < c.EndDate &&
                newComp.EndDate > c.StartDate
            );

            if (conflict)
            {
                return Json(new
                {
                    success = false,
                    message = "You have a schedule on this time and date."
                });
            }

            _context.Competitions.Add(newComp);
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Competition added successfully!"
            });
        }

        public async Task<IActionResult> Dashboard()
        {
            // Total counts
            ViewBag.TotalCoaches = await _context.Coaches.CountAsync();
            ViewBag.TotalAthletes = await _context.Athletes.CountAsync();
            ViewBag.TotalTeams = await _context.Teams.CountAsync();
            ViewBag.TotalCompetitions = await _context.Competitions.CountAsync(); // FIXED ✔

            // competitions model passed to the view
            var competitions = await _context.Competitions.ToListAsync();

            // send teams for dropdown (All Teams)
            ViewBag.Teams = await _context.Teams
                .Where(t => t.Status == "Accepted")
                .Select(t => new { t.TeamId, t.TeamName, t.SportId })
                .ToListAsync();

            // send sports for dropdown (All Sports)
            ViewBag.Sports = await _context.Sports
                .Select(s => new { sportId = s.Id, sportName = s.Name })
                .ToListAsync();

            return View(competitions);
        }
       

        [HttpGet]
        public IActionResult GetAttendanceOverTime(int? teamId = null, int? sessionId = null)
        {
            // Join Attendance -> Athlete -> Session
            var query = _context.Attendance
                .Include(a => a.Athlete)
                .Include(a => a.Session)
                .AsQueryable();

            // Filter by team if provided
            if (teamId.HasValue)
            {
                query = query.Where(a => a.Athlete.TeamId == teamId.Value);
            }

            // Filter by session if provided
            if (sessionId.HasValue)
            {
                query = query.Where(a => a.SessionId == sessionId.Value);
            }

            // Project to anonymous object with date and session
            var attendanceData = query
                .AsEnumerable() // switch to client-side evaluation
                .GroupBy(a => new { a.Session.SessionId, date = a.Session.DateTime.Date })
                .Select(g => new
                {
                    sessionId = g.Key.SessionId,
                    date = g.Key.date,
                    presentCount = g.Count(a => a.Status == AttendanceStatus.Present),
                    totalCount = g.Count()
                })
                .OrderBy(d => d.date)
                .ThenBy(d => d.sessionId)
                .ToList();

            return Json(attendanceData);
        }


        [HttpGet]
        public async Task<IActionResult> GetAthleteLeaderboard()
        {
            var leaderboard = await _context.Performance
                .GroupBy(p => p.AthleteId)
                .Select(g => new
                {
                    AthleteId = g.Key,
                    // Join with Athletes to get name and team
                    AthleteName = _context.Athletes
                                    .Where(a => a.AthleteId == g.Key)
                                    .Select(a => a.FirstName + " " + a.LastName)
                                    .FirstOrDefault(),
                    TeamId = _context.Athletes
                                    .Where(a => a.AthleteId == g.Key)
                                    .Select(a => a.TeamId)
                                    .FirstOrDefault(),
                    TotalPoints = g.Sum(x => x.Rating),
                    MaxPoints = g.Sum(x => x.MaxRating),
                    Percentage = g.Sum(x => x.MaxRating) > 0 ?
                                 (g.Sum(x => x.Rating) * 100.0 / g.Sum(x => x.MaxRating)) : 0
                })
                .OrderByDescending(x => x.TotalPoints)
                .ToListAsync();

            return Json(leaderboard);
        }

        [HttpGet]
        public async Task<IActionResult> GetAthleteGenderDistribution()
        {
            var data = await _context.Athletes
                .GroupBy(a => a.Gender)
                .Select(g => new
                {
                    Gender = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllParticipants()
        {
            var data = await _context.CompetitionParticipants
                .Select(cp => new
                {
                    cp.ParticipantId,
                    cp.CompetitionId,
                    CompetitionName = _context.Competitions
                        .Where(c => c.CompetitionId == cp.CompetitionId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                    cp.TeamId,
                    TeamName = _context.Teams
                        .Where(t => t.TeamId == cp.TeamId)
                        .Select(t => t.TeamName)
                        .FirstOrDefault(),
                    cp.Results,
                    cp.Status
                })
                .ToListAsync();

            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams()
        {
            var teams = await _context.Teams
                .Where(t => t.Status == "Accepted") // optional: only accepted teams
                .Select(t => new
                {
                    teamId = t.TeamId,
                    teamName = t.TeamName,
                    sportId = t.SportId
                })
                .ToListAsync();

            return Json(teams);
        }

        public IActionResult Reports()
        {
            return View();
        }

        // GET: /Report/GetParticipantCompetitions
        public IActionResult GetParticipantCompetitions()
        {
            var data = _context.CompetitionParticipants
                .Include(cp => cp.Competition)
                .Include(cp => cp.Team)
                    .ThenInclude(t => t.Sport)
                        .ThenInclude(s => s.SubCategory)
                .ToList();

            var jsonData = data.Select(cp => new
            {
                cp.ParticipantId,
                cp.CompetitionId,
                CompetitionName = cp.Competition.Name,
                CompetitionStartDate = cp.Competition.StartDate,
                CompetitionEndDate = cp.Competition.EndDate,
                cp.TeamId,
                TeamName = cp.Team.TeamName,
                SportName = cp.Team.Sport.Name,
                SubCategoryName = cp.Team.Sport.SubCategory != null ? cp.Team.Sport.SubCategory.Name : null,
                cp.Results,
                cp.Status
            });

            return Json(jsonData);
        }

        public IActionResult TeamParticipation()
        {
            var participants = _context.CompetitionParticipants
                .Include(cp => cp.Competition)
                .Include(cp => cp.Team)
                .ThenInclude(t => t.Coach)
                .Include(cp => cp.Team)
                .ThenInclude(t => t.Sport) // include Sport
                .ThenInclude(s => s.SubCategory) // include SubCategory for Division
                .Select(cp => new
                {
                    Competition = cp.Competition.Name,
                    DateStart = cp.Competition.StartDate,
                    DateEnd = cp.Competition.EndDate,
                    TeamName = cp.Team.TeamName,
                    Coach = cp.Team.Coach.FirstName + " " + cp.Team.Coach.LastName,
                    Division = cp.Team.Sport.SubCategory.Name, // Men/Women/Mixed
                    ParticipationStatus = cp.Status // "Participating / Not joined / Pending"
                })
                .ToList();

            return Json(participants);
        }

        public IActionResult SportManagement()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetSports()
        {
            var sports = _context.Sports
                .Include(s => s.Category)       // load the Category navigation property
                .Include(s => s.SubCategory)    // load the SubCategory navigation property
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.CategoryId,
                    s.SubCategoryId,
                    s.Status,
                    Category = s.Category != null ? new { s.Category.Id, s.Category.Name } : null,
                    SubCategory = s.SubCategory != null ? new { s.SubCategory.Id, s.SubCategory.Name } : null
                })
                .ToList();

            return Json(new { sports });
        }

        [HttpPost]
        public IActionResult AddSport(string SportName, string Category, string SubCategory)
        {
            if (string.IsNullOrWhiteSpace(SportName))
            {
                TempData["ErrorMessage"] = "Sport name is required.";
                return RedirectToAction("SportManagement");
            }

            // Parse dropdown values
            if (!int.TryParse(Category, out int categoryId) || !int.TryParse(SubCategory, out int subCategoryId))
            {
                TempData["ErrorMessage"] = "Invalid category or subcategory.";
                return RedirectToAction("SportManagement");
            }

            // Get the next ID manually
            int nextId = (_context.Sports.Max(s => (int?)s.Id) ?? 0) + 1;

            var sport = new Sport
            {
                Id = nextId, // manually set the Id
                Name = SportName,
                CategoryId = categoryId,
                SubCategoryId = subCategoryId
            };

            _context.Sports.Add(sport);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Sport added successfully!";
            return RedirectToAction("SportManagement");
        }


        [HttpPost]
        public JsonResult ArchiveSport(int id)
        {
            var sport = _context.Sports.FirstOrDefault(s => s.Id == id);
            if (sport == null)
                return Json(new { success = false, message = "Sport not found." });

            sport.Status = "Archived"; // or sport.IsArchived = true
            _context.SaveChanges();

            return Json(new { success = true, message = $"Sport '{sport.Name}' archived successfully!" });
        }

        [HttpPost]
        public JsonResult RestoreSport(int id)
        {
            var sport = _context.Sports.FirstOrDefault(s => s.Id == id);
            if (sport == null)
                return Json(new { success = false, message = "Sport not found." });

            sport.Status = "Active"; // or whatever represents active
            _context.SaveChanges();

            return Json(new { success = true, message = $"Sport '{sport.Name}' restored successfully!" });
        }


        [HttpPost]
        public async Task<IActionResult> ImportAthletesJson(IFormFile uploadedFile)
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                if (uploadedFile == null || uploadedFile.Length == 0)
                {
                    TempData["Error"] = "No file uploaded.";
                    return RedirectToAction("ManageTeam");
                }

                var uploadedFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "AthleteFiles");
                if (!Directory.Exists(uploadedFilePath))
                    Directory.CreateDirectory(uploadedFilePath);

                var filePath = Path.Combine(uploadedFilePath, uploadedFile.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadedFile.CopyToAsync(stream);
                }

                var athletes = new List<AthleteModel>();
                var duplicateIds = new List<int>();
                var missingDataRows = new List<int>();

                var existingStudentIds = _context.Athletes.Select(a => a.StudentId).ToHashSet();
                var fileStudentIds = new HashSet<int>();

                string extension = Path.GetExtension(filePath).ToLower();

                // ============================================
                // ========== PROCESS EXCEL FILE ==============
                // ============================================
                if (extension == ".xlsx" || extension == ".xls")
                {
                    using (var stream = System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read))
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        bool isFirstRow = true;
                        int excelRowNumber = 1;

                        while (reader.Read())
                        {
                            excelRowNumber++;

                            if (isFirstRow)
                            {
                                isFirstRow = false;
                                continue;
                            }

                            // Skip row if all cells are null/empty
                            bool isEmptyRow = true;
                            for (int i = 0; i < 6; i++)
                            {
                                if (!string.IsNullOrWhiteSpace(reader.GetValue(i)?.ToString()))
                                {
                                    isEmptyRow = false;
                                    break;
                                }
                            }
                            if (isEmptyRow) continue;

                            ProcessRow(
                                reader.GetValue(0)?.ToString(),
                                reader.GetValue(1)?.ToString(),
                                reader.GetValue(2)?.ToString(),
                                reader.GetValue(3)?.ToString(),
                                reader.GetValue(4),
                                reader.GetValue(5)?.ToString(),
                                excelRowNumber,
                                athletes,
                                duplicateIds,
                                missingDataRows,
                                existingStudentIds,
                                fileStudentIds);
                        }

                    }
                }

                // ============================================
                // =========== PROCESS CSV FILE ===============
                // ============================================
                else if (extension == ".csv")
                {
                    var lines = System.IO.File.ReadAllLines(filePath);
                    int rowNumber = 1;

                    foreach (var line in lines.Skip(1)) // skip header
                    {
                        rowNumber++;
                        if (string.IsNullOrWhiteSpace(line)) continue; // skip empty lines

                        var cols = line.Split(',');
                        if (cols.All(c => string.IsNullOrWhiteSpace(c))) continue; // extra safety

                        if (cols.Length < 6)
                        {
                            missingDataRows.Add(rowNumber);
                            continue;
                        }

                        ProcessRow(cols[0], cols[1], cols[2], cols[3], cols[4], cols[5],
                                   rowNumber,
                                   athletes,
                                   duplicateIds,
                                   missingDataRows,
                                   existingStudentIds,
                                   fileStudentIds);
                    }

                }
                else
                {
                    TempData["Error"] = "Invalid file type. Please upload an Excel (.xlsx) or CSV file.";
                    return RedirectToAction("ManageAthlete");
                }

                // Errors?
                if (duplicateIds.Any() || missingDataRows.Any())
                {
                    string message = "";
                    if (duplicateIds.Any())
                        message += $"Duplicate Student ID(s) found: {string.Join(", ", duplicateIds)}. ";
                    if (missingDataRows.Any())
                        message += $"Missing required fields at row(s): {string.Join(", ", missingDataRows)}.";

                    TempData["Error"] = message;
                    return RedirectToAction("ManageAthlete");
                }

                // Save to DB
                _context.Athletes.AddRange(athletes);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"{athletes.Count} athletes imported successfully!";
                return RedirectToAction("ManageAthlete");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error importing athletes: {ex.Message}";
                return RedirectToAction("ManageTeam");
            }
        }


        // ===========================================================
        // ================ SHARED ROW PROCESSOR =====================
        // ===========================================================
        private void ProcessRow(
            string idStr, string fullName, string gender, string yearLevel, object birthdateRaw, string course,
            int rowNumber,
            List<AthleteModel> athletes,
            List<int> duplicateIds,
            List<int> missingDataRows,
            HashSet<int> existingStudentIds,
            HashSet<int> fileStudentIds)
        {
            if (string.IsNullOrWhiteSpace(idStr) ||
                string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(gender) ||
                string.IsNullOrWhiteSpace(yearLevel) ||
                birthdateRaw == null ||
                string.IsNullOrWhiteSpace(birthdateRaw.ToString()))
            {
                missingDataRows.Add(rowNumber);
                return;
            }

            if (!int.TryParse(idStr, out int studentId))
            {
                missingDataRows.Add(rowNumber);
                return;
            }

            if (existingStudentIds.Contains(studentId) || fileStudentIds.Contains(studentId))
            {
                duplicateIds.Add(studentId);
                return;
            }
            fileStudentIds.Add(studentId);

            var parts = fullName.Split(' ', 2);
            var firstName = parts[0];
            var lastName = parts.Length > 1 ? parts[1] : "";

            DateTime? birthdate = null;
            if (DateTime.TryParse(birthdateRaw.ToString(), out DateTime parsed))
                birthdate = parsed;

            athletes.Add(new AthleteModel
            {
                StudentId = studentId,
                FirstName = firstName,
                LastName = lastName,
                Gender = gender,
                YearLevel = int.TryParse(yearLevel, out int yl) ? yl : 0,
                Birthdate = birthdate,
                Course = course
            });
        }
    }
}
