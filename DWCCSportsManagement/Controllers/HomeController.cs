using DWCCSportsManagement.Data;
using DWCCSportsManagement.Models;
using DWCCSportsManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace DWCCSportsManagement.Controllers
{
    [Authorize(Roles = "Coach")]
    public class HomeController : Controller
    {
        private readonly SportsContext _context;

        public HomeController(SportsContext context)
        {
            _context = context;
        }

        // ============================================
        // LOGIN (GET)
        // ============================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // ============================================
        // LOGIN (POST)
        // ============================================
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Index(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter username and password.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user != null)
            {
                if (user.Status != "Active")
                {
                    ViewBag.Error = "Your account is inactive. Contact the administrator.";
                    return View();
                }

                var hasher = new PasswordHasher<User>();
                var result = hasher.VerifyHashedPassword(user, user.Password, password);

                if (result == PasswordVerificationResult.Success)
                {
                    string role = (user.Role ?? "").Trim().ToLower();
                    string finalRole = role switch
                    {
                        "admin" => "Admin",
                        "team manager" => "Manager",
                        "manager" => "Manager",
                        "coach" => "Coach",
                        _ => "Coach"
                    };

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Role, finalRole)
                    };

                    var identity = new ClaimsIdentity(claims,
                        CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity),
                        new AuthenticationProperties { IsPersistent = true });

                    return finalRole switch
                    {
                        "Admin" => RedirectToAction("AdminInterface", "Admin"),
                        "Manager" => RedirectToAction("ManageTeam", "TeamManager"),
                        _ => RedirectToAction("Roster")
                    };
                }
            }

            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        // ============================================
        // LOGOUT
        // ============================================
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        // ============================================
        // FORGOT PASSWORD — GET
        // ============================================
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // ============================================
        // FORGOT PASSWORD — POST
        // ============================================
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter a valid email.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                ViewBag.Success = "If the email exists, a reset code has been sent.";
                return View();
            }

            string code = new Random().Next(100000, 999999).ToString();

            var reset = new PasswordReset
            {
                UserId = user.Id,
                ResetCode = code,
                ExpiryTime = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            };

            var old = _context.PasswordResets.Where(r => r.UserId == user.Id && !r.IsUsed).ToList();
            _context.PasswordResets.RemoveRange(old);

            _context.PasswordResets.Add(reset);
            _context.SaveChanges();

            var mail = HttpContext.RequestServices.GetRequiredService<IEmailService>();
            await mail.SendPasswordResetCodeAsync(email, code);

            return RedirectToAction("VerifyResetCode", new { email });
        }

        // ============================================
        // VERIFY RESET CODE — GET
        // ============================================
        [AllowAnonymous]
        public IActionResult VerifyResetCode(string email)
        {
            if (email == null) return RedirectToAction("Index");

            ViewBag.Email = email;
            return View();
        }

        // ============================================
        // VERIFY RESET CODE — POST
        // ============================================
        [AllowAnonymous]
        [HttpPost]
        public IActionResult VerifyResetCode(string email, string resetCode)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(resetCode))
            {
                ViewBag.Error = "Enter both email and reset code.";
                ViewBag.Email = email;
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ViewBag.Error = "Invalid email.";
                return View();
            }

            var valid = _context.PasswordResets.FirstOrDefault(r =>
                r.UserId == user.Id &&
                r.ResetCode == resetCode &&
                !r.IsUsed &&
                r.ExpiryTime > DateTime.UtcNow);

            if (valid == null)
            {
                ViewBag.Error = "Invalid or expired reset code.";
                ViewBag.Email = email;
                return View();
            }

            valid.IsUsed = true;
            _context.SaveChanges();

            return RedirectToAction("ResetPassword", new { email, userId = user.Id });
        }

        // ============================================
        // RESET PASSWORD — GET
        // ============================================
        [AllowAnonymous]
        public IActionResult ResetPassword(string email, int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId && u.Email == email);
            if (user == null) return RedirectToAction("Index");

            ViewBag.Email = email;
            ViewBag.UserId = userId;
            return View();
        }
        // ======================================================
        // ATTENDANCE PAGE (GET) — FINAL FIXED VERSION
        // ======================================================

        [HttpGet]
        public IActionResult Attendance(int? sessionId)
        {
            string coachUsername = User.Identity.Name;

            var coach = _context.Coaches.FirstOrDefault(c => c.Username == coachUsername);
            if (coach == null)
            {
                TempData["Error"] = "Coach not found.";
                return RedirectToAction("Index");
            }

            var sessions = _context.Sessions
                .Include(s => s.Team)
                .Where(s => s.Team.CoachId == coach.Id)
                .OrderByDescending(s => s.DateTime)
                .ToList();

            ViewBag.Sessions = sessions;
            ViewBag.SelectedSessionId = sessionId;

            if (!sessionId.HasValue)
                return View(new List<AthleteModel>());

            var selectedSession = sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (selectedSession == null)
                return View(new List<AthleteModel>());

            int teamId = selectedSession.TeamId;

            var athletes = _context.Athletes
                .Where(a => a.TeamId == teamId)
                .OrderBy(a => a.LastName)
                .ToList();

            // Load attendance
            ViewBag.ExistingAttendance = _context.Attendance
                .Where(a => a.SessionId == sessionId)
                .ToDictionary(a => a.AthleteId, a => a.Status);

            // Load ratings
            ViewBag.ExistingRatings = _context.Performances
    .Where(p => p.SessionId == sessionId)
    .GroupBy(p => p.AthleteId)
    .ToDictionary(
        g => g.Key,
        g => g.OrderByDescending(p => p.Id).First().Rating
    );


            // Load SCHOLARSHIP per athlete
            ViewBag.ExistingScholarships = _context.Performances
    .Where(p => p.SessionId == sessionId)
    .GroupBy(p => p.AthleteId)
    .ToDictionary(
        g => g.Key,
        g => g.OrderByDescending(p => p.Id).First().ScholarshipPercentage
    );


            // Load 5 saved rubric criteria names
            ViewBag.RubricCriteria = _context.Rubrics
                .GroupBy(r => r.Criteria)
                .Select(g => g.Key)
                .Take(5)
                .ToList();

            return View(athletes);
        }


        [HttpGet]

        [HttpGet]
        public IActionResult ScheduleTraining()
        {
            string coachUsername = User.Identity.Name;

            var coach = _context.Coaches.FirstOrDefault(c => c.Username == coachUsername);
            if (coach == null)
            {
                TempData["Error"] = "Coach not found.";
                return RedirectToAction("Index");
            }

            // LOAD ALL COACH'S SESSIONS
            var sessions = _context.Sessions
                .Include(s => s.Team)
                .Where(s => s.Team.CoachId == coach.Id)
                .OrderByDescending(s => s.DateTime)
                .ToList();

            // LOAD TEAMS FOR DROPDOWN   ⬅⬅ THE MISSING PART
            ViewBag.TeamNames = _context.Teams
                .Include(t => t.Sport)
                .Where(t => t.CoachId == coach.Id && t.Status == "Accepted")
                .ToList();

            return View(sessions);
        }

        [HttpGet]
        public IActionResult CoachProfile()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Index");

            var coach = _context.Coaches
                .FirstOrDefault(c => c.Username == username);

            if (coach == null)
            {
                TempData["Error"] = "Coach profile not found.";
                return RedirectToAction("Dashboard");
            }

            return View(coach);
        }

        [HttpPost]
     
        public IActionResult ArchiveSession(int id)
        {
            var session = _context.Sessions.FirstOrDefault(s => s.SessionId == id);

            if (session == null)
                return NotFound();

            session.Status = "Archived"; // MOVE TO ARCHIVED
            _context.SaveChanges();

            TempData["Success"] = "Session archived successfully!";
            return RedirectToAction("ScheduleTraining");
        }






        // ======================================================
        // SAVE ATTENDANCE (POST)
        // Includes: select-all, rubric rating save, remarks auto
        // ======================================================
        [HttpPost]
        public IActionResult SaveAttendance(
     int SessionId,
     Dictionary<int, string> Statuses,
     Dictionary<int, int?> Ratings,
     Dictionary<int, int?> Scholarship,   // ⭐ FIXED NAME
     int maxRating = 25)
        {
            try
            {
                if (SessionId == 0)
                {
                    TempData["Error"] = "Invalid session.";
                    return RedirectToAction("Attendance");
                }

                var allAthletes = _context.Athletes
                    .Where(a => a.TeamId == _context.Sessions
                        .First(s => s.SessionId == SessionId).TeamId)
                    .ToList();

                // SELECT-ALL feature
                if (Statuses.ContainsKey(-1))
                {
                    string value = Statuses[-1];

                    if (value is "Present" or "Late" or "Absent" or "Excused")
                    {
                        Statuses = allAthletes.ToDictionary(
                            a => a.AthleteId,
                            a => value
                        );
                    }
                }

                foreach (var item in Statuses)
                {
                    int athleteId = item.Key;
                    string statusValue = item.Value;

                    if (!Enum.TryParse(statusValue, out AttendanceStatus status))
                        status = AttendanceStatus.Present;

                    var existing = _context.Attendance
                        .FirstOrDefault(a => a.SessionId == SessionId && a.AthleteId == athleteId);

                    if (existing == null)
                    {
                        _context.Attendance.Add(new AttendanceModel
                        {
                            AthleteId = athleteId,
                            SessionId = SessionId,
                            Status = status
                        });
                    }
                    else
                    {
                        existing.Status = status;
                    }

                    // ----------------------------
                    // PERFORMANCE SAVE SECTION
                    // ----------------------------
                    int rating = Ratings != null && Ratings.TryGetValue(athleteId, out var r) && r.HasValue
                        ? r.Value
                        : 0;

                    string remark = GetPerformanceRemark(rating);

                    // ⭐ READ SCHOLARSHIP (NULL SAFE)
                    int scholarshipValue = Scholarship != null &&
                                           Scholarship.TryGetValue(athleteId, out var sch) &&
                                           sch.HasValue
                        ? sch.Value
                        : 0;

                    var perf = _context.Performances
                        .FirstOrDefault(p => p.SessionId == SessionId && p.AthleteId == athleteId);

                    if (perf == null)
                    {
                        _context.Performances.Add(new PerformanceModel
                        {
                            AthleteId = athleteId,
                            SessionId = SessionId,
                            Rating = rating,
                            MaxRating = 25,
                            Remarks = remark,
                            ScholarshipPercentage = scholarshipValue   // ⭐ FIXED
                        });
                    }
                    else
                    {
                        perf.Rating = rating;
                        perf.MaxRating = 25;
                        perf.Remarks = remark;
                        perf.ScholarshipPercentage = scholarshipValue;  // ⭐ FIXED
                    }
                }

                _context.SaveChanges();
                TempData["Success"] = "Attendance, ratings, and scholarship saved!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error saving attendance: " + ex.Message;
            }

            return RedirectToAction("Attendance", new { sessionId = SessionId });
        }



        // ======================================================
        // AUTO-GENERATE REMARKS (25-point scale)
        // ======================================================
        private string GetPerformanceRemark(int rating)
        {
            if (rating >= 21) return "Excellent Student-athlete";
            if (rating >= 16) return "Good Student-athlete";
            if (rating >= 11) return "Average Student-athlete";
            if (rating >= 6) return "Fair Student-athlete";
            return "Poor Student-athlete";
        }
        // ======================================================
        // GET RUBRICS (5×5 FIXED FORMAT)
        // Returns ALL saved rubric rows
        // ======================================================
        [HttpGet]
        public IActionResult GetRubrics()
        {
            var rubrics = _context.Rubrics
                .OrderBy(r => r.Criteria)
                .ThenByDescending(r => r.Points)
                .Select(r => new
                {
                    r.Criteria,
                    r.Points,
                    r.Description
                })
                .ToList();

            return Json(rubrics);
        }


        // ======================================================
        // SAVE RUBRICS (COMPLETE WORKING VERSION)
        // Your modal sends 25 rows (5 criteria × 5 points)
        // ======================================================
        [HttpPost]
        public IActionResult SaveRubrics([FromBody] List<RubricSaveModel> rubrics)
        {
            if (rubrics == null || rubrics.Count == 0)
                return Json("No rubrics submitted.");

            try
            {
                // Clear old rubrics before saving new ones
                var oldRubrics = _context.Rubrics.ToList();
                _context.Rubrics.RemoveRange(oldRubrics);
                _context.SaveChanges();

                foreach (var item in rubrics)
                {
                    _context.Rubrics.Add(new Rubric
                    {
                        Criteria = item.Criteria,
                        Points = item.Points,
                        Description = item.Description
                    });
                }

                _context.SaveChanges();
                return Json("Rubrics saved successfully!");
            }
            catch (Exception ex)
            {
                return Json("Error saving rubrics: " + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult AddSession(DateTime DateTime, int TeamId, string Description, string Location)
        {
            if (TeamId == 0)
            {
                TempData["Error"] = "Please select a team.";
                return RedirectToAction("ScheduleTraining");
            }

            var session = new SessionModel
            {
                TeamId = TeamId,
                DateTime = DateTime,
                Description = Description,
                Location = Location,
                Status = "Active"
            };

            _context.Sessions.Add(session);
            _context.SaveChanges();

            TempData["Success"] = "Session added successfully!";
            return RedirectToAction("ScheduleTraining");
        }



        // ======================================================
        // MODEL ENTRY FOR RUBRIC SAVE API
        // ======================================================
        public class RubricSaveModel
        {
            public string Criteria { get; set; }
            public int Points { get; set; }
            public string Description { get; set; }
        }


        // ======================================================
        // GET ATHLETE BY ID — EDIT MODAL LOAD
        // ======================================================
        public IActionResult GetAthleteById(int id)
        {
            var athlete = _context.Athletes
                .Where(a => a.AthleteId == id)
                .Select(a => new
                {
                    a.AthleteId,
                    a.FirstName,
                    a.LastName,
                    a.StudentId,
                    a.Gender,
                    a.Birthdate,
                    a.Status,
                    a.TeamId,
                    a.SportId,
                    a.MedicalInfo,
                    a.Grades
                })
                .FirstOrDefault();

            return Json(athlete);
        }


        // ======================================================
        // UPDATE ATHLETE (EDIT MODAL SAVE)
        // ======================================================
        [HttpPost]
        public IActionResult UpdateAthlete([FromBody] AthleteModel model)
        {
            if (model == null || model.AthleteId == 0)
                return Json(new { success = false, message = "Invalid athlete data." });

            var athlete = _context.Athletes.FirstOrDefault(a => a.AthleteId == model.AthleteId);
            if (athlete == null)
                return Json(new { success = false, message = "Athlete not found." });

            try
            {
                athlete.FirstName = model.FirstName;
                athlete.LastName = model.LastName;
                athlete.StudentId = model.StudentId;
                athlete.Gender = model.Gender;
                athlete.Status = model.Status;
                athlete.Birthdate = model.Birthdate;

                athlete.TeamId = model.TeamId;
                athlete.SportId = model.SportId;

                _context.SaveChanges();

                return Json(new { success = true, message = "Athlete updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // ======================================================
        // UPLOAD MEDICAL FILE
        // ======================================================
        [HttpPost]
        public async Task<IActionResult> UploadMedicalInfo(int id, IFormFile MedicalFile)
        {
            if (MedicalFile == null)
            {
                TempData["Error"] = "No file selected.";
                return RedirectToAction("Roster");
            }

            var athlete = await _context.Athletes.FindAsync(id);
            if (athlete == null) return NotFound();

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            Directory.CreateDirectory(uploads);

            string filename = Guid.NewGuid() + Path.GetExtension(MedicalFile.FileName);
            string filepath = Path.Combine(uploads, filename);

            using (var stream = new FileStream(filepath, FileMode.Create))
                await MedicalFile.CopyToAsync(stream);

            athlete.MedicalInfo = "/images/" + filename;
            _context.SaveChanges();

            TempData["Success"] = "Medical info uploaded!";
            return RedirectToAction("Roster");
        }


        // ======================================================
        // UPLOAD GRADES FILE
        // ======================================================
        [HttpPost]
        public async Task<IActionResult> UploadGrades(int id, IFormFile GradesFile)
        {
            if (GradesFile == null)
            {
                TempData["Error"] = "No file selected.";
                return RedirectToAction("Roster");
            }

            var athlete = await _context.Athletes.FindAsync(id);
            if (athlete == null) return NotFound();

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            Directory.CreateDirectory(uploads);

            string filename = Guid.NewGuid() + Path.GetExtension(GradesFile.FileName);
            string filepath = Path.Combine(uploads, filename);

            using (var stream = new FileStream(filepath, FileMode.Create))
                await GradesFile.CopyToAsync(stream);

            athlete.Grades = "/images/" + filename;
            _context.SaveChanges();

            TempData["Success"] = "Grades uploaded!";
            return RedirectToAction("Roster");
        }
        // ==============================================
        //   GET REMARKS / PERFORMANCE HISTORY (per athlete)
        // ==============================================
        [HttpGet]
        public async Task<IActionResult> GetRemarks(int athleteId)
        {
            // Get all performance records for this athlete
            var performances = await _context.Performances
                .Where(p => p.AthleteId == athleteId)
                .Join(_context.Sessions,
                      p => p.SessionId,
                      s => s.SessionId,
                      (p, s) => new
                      {
                          SessionName = s.Description,
                          s.DateTime,
                          p.Rating,
                          p.MaxRating,
                          p.Remarks,
                          p.ScholarshipPercentage
                      })
                .OrderBy(p => p.DateTime)
                .ToListAsync();

            // LATEST SCHOLARSHIP %
            int latestScholarship = performances
                .Where(p => p.ScholarshipPercentage.HasValue)
                .OrderByDescending(p => p.DateTime)
                .Select(p => p.ScholarshipPercentage.Value)
                .FirstOrDefault();

            // Get athlete name
            var athlete = await _context.Athletes
                .Where(a => a.AthleteId == athleteId)
                .Select(a => a.FirstName + " " + a.LastName)
                .FirstOrDefaultAsync();

            return Json(new
            {
                athleteName = athlete,
                sessions = performances,
                latestScholarship = latestScholarship
            });
        }



        // ==============================================
        //           GET ATTENDANCE RECORDS (per athlete)
        // ==============================================
        public IActionResult GetAttendance(int athleteId)
        {
            var attendance = _context.Attendance
                .Where(a => a.AthleteId == athleteId)
                .Select(a => new
                {
                    a.AttendanceId,
                    SessionName = a.Session.Description,
                    a.Session.DateTime,
                    Status = a.Status.ToString()
                })
                .OrderBy(a => a.DateTime)
                .ToList();

            return Json(attendance);
        }


        // ==========================================================
        // GET TEAMS + SPORTS (Used in Add/Edit Athlete modal)
        // ==========================================================
        [HttpGet]
        public IActionResult GetTeamsAndSports()
        {
            var teams = _context.Teams
                .Select(t => new { t.TeamId, t.TeamName, t.SportId })
                .ToList();

            var sports = _context.Sports
                .Select(s => new { s.Id, s.Name })
                .ToList();

            return Json(new
            {
                teams = teams,
                sports = sports
            });
        }


        // ==========================================================
        // SEARCH ATHLETES WITHOUT TEAM (Enrollment Feature)
        // ==========================================================
        [HttpGet]
        public JsonResult SearchAthletesWithoutTeam(string searchTerm)
        {
            try
            {
                int numericSearch = 0;
                bool isNumeric = int.TryParse(searchTerm, out numericSearch);

                var athletes = _context.Athletes
                    .Where(a => a.TeamId == null && (
                        (isNumeric && a.StudentId == numericSearch) ||
                        a.FirstName.Contains(searchTerm) ||
                        a.LastName.Contains(searchTerm)
                    ))
                    .Select(a => new
                    {
                        a.AthleteId,
                        a.FirstName,
                        a.LastName,
                        a.StudentId,
                        a.Gender,
                        a.Birthdate,
                        a.Course,
                        a.YearLevel,
                        a.MedicalInfo,
                        a.Grades
                    })
                    .Take(10)
                    .ToList();

                return Json(athletes);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }


        // ======================================================
        //                      DASHBOARD VIEW
        // ======================================================
        public IActionResult Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out int coachId))
                return RedirectToAction("Index");

            var teamIds = _context.Teams
                .Where(t => t.CoachId == coachId)
                .Select(t => t.TeamId)
                .ToList();

            var athletes = _context.Athletes
                .Where(a => a.TeamId.HasValue && teamIds.Contains(a.TeamId.Value))
                .ToList();

            ViewBag.UserId = userId;
            return View(athletes);
        }


        // ======================================================
        //                 DASHBOARD DATA API
        // ======================================================
        [HttpGet]
        public IActionResult GetDashboardData()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(id, out int coachId))
                return Json(new { error = "Invalid coach ID" });

            var teams = _context.Teams.Where(t => t.CoachId == coachId).ToList();
            var teamIds = teams.Select(t => t.TeamId).ToList();

            var athletes = _context.Athletes
                .Where(a => a.TeamId.HasValue && teamIds.Contains(a.TeamId.Value))
                .ToList();

            var sports = _context.Sports.ToList();

            var genderData = athletes
                .GroupBy(a => a.Gender)
                .Select(g => new { gender = g.Key, count = g.Count() })
                .ToList();

            var statusData = athletes
                .GroupBy(a => a.Status)
                .Select(g => new { status = g.Key, count = g.Count() })
                .ToList();

            var perTeam = athletes
                .GroupBy(a => a.TeamId)
                .Select(g => new
                {
                    teamId = g.Key,
                    teamName = teams.FirstOrDefault(t => t.TeamId == g.Key)?.TeamName,
                    count = g.Count()
                })
                .ToList();

            var perSport = athletes
                .GroupBy(a => a.SportId)
                .Select(g => new
                {
                    sportId = g.Key,
                    sportName = sports.FirstOrDefault(s => s.Id == g.Key)?.Name,
                    count = g.Count()
                })
                .ToList();

            return Json(new
            {
                gender = genderData,
                status = statusData,
                perTeam,
                perSport
            });
        }
        // ======================================================
        //             COMPETITION PARTICIPATION VIEW
        // ======================================================
        public IActionResult CompetitionParticipation()
        {
            // List of competitions
            var competitions = _context.Competitions.ToList();

            // Teams accepted under the school
            var teams = _context.Teams
                .Where(t => t.Status == "Accepted")
                .Include(t => t.Sport)
                    .ThenInclude(s => s.SubCategory)
                .ToList();

            // Existing participants
            var participants = _context.CompetitionParticipants.ToList();

            // Build dictionary of available teams PER competition
            var available = new Dictionary<int, List<object>>();

            foreach (var comp in competitions)
            {
                // IDs of teams already joined
                var joinedTeamIds = participants
                    .Where(p => p.CompetitionId == comp.CompetitionId)
                    .Select(p => p.TeamId)
                    .ToList();

                // Teams not yet joined
                available[comp.CompetitionId] = teams
                    .Where(t => !joinedTeamIds.Contains(t.TeamId))
                    .Select(t => new
                    {
                        t.TeamId,
                        t.TeamName,
                        SubCategory = t.Sport?.SubCategory?.Name ?? "N/A"
                    })
                    .Cast<object>()
                    .ToList();
            }

            ViewBag.AvailableTeamsByCompetition = available;

            // ViewModel for strong typing
            return View(new CompetitionParticipationViewModel
            {
                Competitions = competitions,
                Teams = teams,
                Participants = participants
            });
        }


        [HttpPost]
        public IActionResult AddAthlete(AthleteModel model, IFormFile MedicalFile, IFormFile GradesFile)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error1"] = "Invalid athlete data.";
                return RedirectToAction("Roster");
            }

            // Make sure Student ID is not duplicated
            if (_context.Athletes.Any(a => a.StudentId == model.StudentId))
            {
                TempData["Error1"] = "Student ID already exists!";
                return RedirectToAction("Roster");
            }

            // Save files
            string medicalPath = null;
            string gradesPath = null;

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
            Directory.CreateDirectory(uploads);

            if (MedicalFile != null)
            {
                string filename = Guid.NewGuid() + Path.GetExtension(MedicalFile.FileName);
                string filepath = Path.Combine(uploads, filename);
                using var stream = new FileStream(filepath, FileMode.Create);
                MedicalFile.CopyTo(stream);
                medicalPath = "/images/" + filename;
            }

            if (GradesFile != null)
            {
                string filename = Guid.NewGuid() + Path.GetExtension(GradesFile.FileName);
                string filepath = Path.Combine(uploads, filename);
                using var stream = new FileStream(filepath, FileMode.Create);
                GradesFile.CopyTo(stream);
                gradesPath = "/images/" + filename;
            }

            model.MedicalInfo = medicalPath;
            model.Grades = gradesPath;

            // Save athlete
            _context.Athletes.Add(model);
            _context.SaveChanges();

            TempData["Success1"] = "Athlete added successfully!";
            return RedirectToAction("Roster");
        }


        // ======================================================
        //        ADD TEAM(S) TO COMPETITION
        // ======================================================
        [HttpPost]
        public IActionResult ParticipateInCompetition(int competitionId, List<int> TeamIds)
        {
            if (TeamIds == null || TeamIds.Count == 0)
            {
                TempData["ErrorMessage"] = "No team selected.";
                return RedirectToAction("CompetitionParticipation");
            }

            foreach (var id in TeamIds)
            {
                // Prevent duplicates
                bool exists = _context.CompetitionParticipants
                    .Any(p => p.CompetitionId == competitionId && p.TeamId == id);

                if (!exists)
                {
                    _context.CompetitionParticipants.Add(new CompetitionParticipant
                    {
                        CompetitionId = competitionId,
                        TeamId = id,
                        Results = "N/A",
                        Status = "Pending"
                    });
                }
            }

            _context.SaveChanges();
            TempData["SuccessMessage"] = "Teams successfully added!";
            return RedirectToAction("CompetitionParticipation");
        }


        // ======================================================
        //                UPDATE COMPETITION RESULT
        // ======================================================
        [HttpPost]
        public IActionResult UpdateResult(int participantId, string results)
        {
            var participant = _context.CompetitionParticipants
                .FirstOrDefault(p => p.ParticipantId == participantId);

            if (participant != null)
            {
                participant.Results = results;
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Result updated!";
            }

            return RedirectToAction("CompetitionParticipation");
        }


        // ======================================================
        //         UPDATE PARTICIPANT STATUS (WINNER, etc.)
        // ======================================================
        [HttpPost]
        public IActionResult UpdateStatus(int ParticipantId, string Status)
        {
            var p = _context.CompetitionParticipants
                .FirstOrDefault(x => x.ParticipantId == ParticipantId);

            if (p != null)
            {
                p.Status = Status;
                _context.SaveChanges();
                TempData["Success"] = "Status updated.";
            }

            return RedirectToAction("CompetitionParticipation");
        }


        // ======================================================
        //         REMOVE PARTICIPANT FROM COMPETITION
        // ======================================================
        [HttpPost]
        public IActionResult WithdrawParticipant(int participantId)
        {
            var p = _context.CompetitionParticipants
                .FirstOrDefault(x => x.ParticipantId == participantId);

            if (p != null)
            {
                _context.CompetitionParticipants.Remove(p);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Team withdrawn.";
            }

            return RedirectToAction("CompetitionParticipation");
        }
        // ======================================================
        //                      REPORTS VIEW
        // ======================================================
        public IActionResult Reports()
        {
            return View();
        }
        public IActionResult Roster(int? sportId = null)
        {
            var username = User.Identity.Name;

            // Find coach logged in
            var coach = _context.Coaches.FirstOrDefault(c => c.Username == username);
            if (coach == null)
            {
                TempData["Error"] = "Coach not found.";
                return RedirectToAction("Index");
            }

            var teams = _context.Teams
                .Include(t => t.Sport)
                .Where(t => t.CoachId == coach.Id)
                .ToList();

            if (sportId.HasValue)
                teams = teams.Where(t => t.SportId == sportId.Value).ToList();

            var teamIds = teams.Select(t => t.TeamId).ToList();
            var athletes = _context.Athletes
                .Include(a => a.Team)
                .Include(a => a.Sport)
                .Where(a => a.TeamId.HasValue && teamIds.Contains(a.TeamId.Value))
                .ToList();

            ViewBag.Sports = teams.Select(t => t.Sport).Distinct().ToList();
            ViewBag.SelectedSport = sportId;
            ViewBag.Teams = teams;

            return View(athletes);
        }



        // ======================================================
        //         REPORT — PARTICIPANT COMPETITIONS
        // ======================================================
        public IActionResult GetParticipantCompetitions()
        {
            var data = _context.CompetitionParticipants
                .Include(cp => cp.Competition)
                .Include(cp => cp.Team)
                    .ThenInclude(t => t.Sport)
                        .ThenInclude(s => s.SubCategory)
                .Where(cp => cp.Status == "Completed")   // Only completed competitions
                .ToList();

            var result = data.Select(cp => new
            {
                cp.ParticipantId,
                cp.CompetitionId,
                CompetitionName = cp.Competition.Name,
                cp.Competition.StartDate,
                cp.Competition.EndDate,
                cp.TeamId,
                TeamName = cp.Team.TeamName,
                SportName = cp.Team.Sport.Name,
                SubCategoryName = cp.Team.Sport.SubCategory?.Name,
                cp.Results,
                cp.Status
            });

            return Json(result);
        }


        // ======================================================
        //          REPORT — ATTENDANCE SUMMARY
        // ======================================================
        public IActionResult GetAttendanceReport()
        {
            var records = _context.Attendance
                .Include(a => a.Athlete)
                .Include(a => a.Session)
                    .ThenInclude(s => s.Team)
                .OrderBy(a => a.Session.DateTime)
                .ToList();

            var data = records.Select(a => new
            {
                a.AttendanceId,
                Date = a.Session.DateTime.ToString("yyyy-MM-dd HH:mm"),
                PlayerName = a.Athlete.FirstName + " " + a.Athlete.LastName,
                TeamName = a.Session.Team.TeamName,
                SessionType = a.Session.Description,
                Status = a.Status.ToString()
            });

            return Json(data);
        }

        // ======================================================
        //         UPDATE ALL RESULTS (BULK SAVE)
        // ======================================================
        // ======================================================
        //         UPDATE ALL RESULTS AND STATUS (BULK SAVE)
        // ======================================================
        [HttpPost]
        public IActionResult UpdateAllResultsAndStatus([FromBody] List<ParticipantResultStatusUpdateModel> updates)
        {
            if (updates == null || updates.Count == 0)
            {
                return Json(new { success = false, message = "No data received." });
            }

            try
            {
                foreach (var update in updates)
                {
                    var participant = _context.CompetitionParticipants
                        .FirstOrDefault(p => p.ParticipantId == update.participantId &&
                                             p.CompetitionId == update.competitionId);

                    if (participant != null)
                    {
                        // Update results if provided
                        if (!string.IsNullOrEmpty(update.results))
                        {
                            participant.Results = update.results;
                        }

                        // Update status if provided
                        if (!string.IsNullOrEmpty(update.status))
                        {
                            participant.Status = update.status;
                        }
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true, message = "All changes saved successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Model for bulk update of results AND status
        public class ParticipantResultStatusUpdateModel
        {
            public int participantId { get; set; }
            public int competitionId { get; set; }
            public string results { get; set; }
            public string status { get; set; }
        }

        // ======================================================
        //                CLEAN FALLBACK ROUTE
        // ======================================================
        public IActionResult NotFoundPage()
        {
            return View("NotFound");
        }
    }
}

