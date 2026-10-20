using DWCCSportsManagement.Data;
using DWCCSportsManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace DWCCSportsManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly SportsContext _context;

        public AdminController(SportsContext context)
        {
            _context = context;
        }

        // GET: Manage Coaches
        public IActionResult ManageCoach(string searchName, string roleFilter, string statusFilter)
        {
            // Filtered coaches for ManageCoach panel
            var coaches = string.IsNullOrEmpty(searchName)
                ? _context.Coaches.ToList()
                : _context.Coaches
                    .Where(c => c.Username.Contains(searchName) || c.FirstName.Contains(searchName) || c.LastName.Contains(searchName))
                    .ToList();

            // Load the teams for each coach
            var coachesWithTeams = coaches.Select(c => new {
                Id = c.Id,
                Username = c.Username,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone = c.Phone,
                ImageUrl = c.ImageUrl,
                Bio = c.Bio,
                Status = c.Status,
                Teams = _context.Teams
                            .Where(t => t.CoachId == c.Id)
                            .Select(t => new { TeamName = t.TeamName })
                            .ToList()
            }).ToList();

            ViewBag.CoachesJson = JsonSerializer.Serialize(coachesWithTeams);

            // Current admin username
            var currentUsername = User?.Identity?.Name ?? string.Empty;

            // Get all users (for Role Manager simplified table)
            var usersQuery = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                usersQuery = usersQuery.Where(u =>
                    u.Username.Contains(searchName) ||
                    u.FirstName.Contains(searchName) ||
                    u.LastName.Contains(searchName) ||
                    (u.Email != null && u.Email.Contains(searchName))
                );
            }
            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                usersQuery = usersQuery.Where(u => u.Role == roleFilter);
            }
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                usersQuery = usersQuery.Where(u => u.Status == statusFilter);
            }
            // Exclude currently logged-in admin from Users list
            if (!string.IsNullOrEmpty(currentUsername))
            {
                usersQuery = usersQuery.Where(u => u.Username != currentUsername);
            }
            var users = usersQuery.ToList();

            // Expose filters in ViewBag
            ViewBag.SelectedRole = roleFilter;
            ViewBag.SelectedStatus = statusFilter;

            // Keep existing combined list for legacy role table if still referenced
            var combinedList = new List<dynamic>();

            foreach (var user in users)
            {
                combinedList.Add(new
                {
                    Id = user.Id,
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Password = user.Password,
                    Status = user.Status,
                    Role = user.Role,
                    Email = user.Email,
                    Source = "User"
                });
            }

            // Also list coaches not present in users (legacy display only)
            var allCoaches = string.IsNullOrEmpty(searchName)
                ? _context.Coaches.ToList()
                : _context.Coaches
                    .Where(c => c.Username.Contains(searchName) || c.FirstName.Contains(searchName) || c.LastName.Contains(searchName))
                    .ToList();

            ViewBag.AllCoaches = allCoaches;

            foreach (var coach in allCoaches)
            {
                if (!users.Any(u => u.Username == coach.Username))
                {
                    combinedList.Add(new
                    {
                        Id = coach.Id,
                        Username = coach.Username,
                        FirstName = coach.FirstName,
                        LastName = coach.LastName,
                        Password = "(Coach Account)",
                        Role = "Coach",
                        Status = coach.Status,
                        Email = (string)null,
                        Source = "Coach"
                    });
                }
            }

            ViewBag.AllAccounts = combinedList;
            ViewBag.UsersOnly = users.Select(u => new {
                u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Name = (u.FirstName + " " + u.LastName).Trim(),
                u.Username,
                u.Email,
                u.Role,
                u.Status
            }).ToList();

            return View(coaches);
        }

        public IActionResult InactivateCoach(int id)
        {
            var coach = _context.Coaches.Find(id);
            if (coach == null)
            {
                TempData["Error"] = "Coach not found.";
                return RedirectToAction("ManageCoach");
            }

            // Check if coach has assigned teams
            var teams = _context.Teams
                .Where(t => t.CoachId == coach.Id)
                .Select(t => new { t.TeamId, t.TeamName })
                .ToList();

            if (teams.Any())
            {
                // Serialize data to show reassign modal
                TempData["ReassignTeams"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    CoachId = coach.Id,
                    CoachName = $"{coach.FirstName} {coach.LastName}",
                    Teams = teams,
                    Action = "Inactivate"
                }, new System.Text.Json.JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                TempData["Error"] = $"Cannot inactivate {coach.FirstName} {coach.LastName} because they have assigned teams. <a href='#' data-bs-toggle='modal' data-bs-target='#reassignTeamsModal'>Reassign Teams</a>";
                return RedirectToAction("ManageCoach");
            }

            // If no teams, safely set status to inactive
            coach.Status = "Inactive";
            _context.SaveChanges();

            TempData["Success"] = $"Coach {coach.FirstName} {coach.LastName} has been set to Inactive.";
            return RedirectToAction("ManageCoach");
        }

        [HttpPost]
        public IActionResult EditCoach(EditCoachViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid data submitted.";
                return RedirectToAction("ManageCoach");
            }

            var coach = _context.Coaches.Find(model.Id);
            if (coach == null) return NotFound();

            // Update base fields (except status)
            coach.Username = model.Username;
            coach.FirstName = model.FirstName;
            coach.LastName = model.LastName;
            coach.Phone = model.Phone;
            coach.Bio = model.Bio;

            // Handle file upload
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var fileName = Path.GetFileName(model.ImageFile.FileName);
                var filePath = Path.Combine("wwwroot/assets/coaches", fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                model.ImageFile.CopyTo(stream);

                coach.ImageUrl = "/assets/coaches/" + fileName;
            }

            // BEFORE UPDATING STATUS → Check if status change is allowed
            bool isChangingToInactive = coach.Status != "Inactive" && model.Status == "Inactive";

            if (isChangingToInactive)
            {
                var teams = _context.Teams
                    .Where(t => t.CoachId == coach.Id)
                    .Select(t => new { t.TeamId, t.TeamName })
                    .ToList();

                if (teams.Any())
                {
                    // STOP status change — do NOT update coach.Status
                    TempData["ReassignTeams"] = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        CoachId = coach.Id,
                        CoachName = $"{coach.FirstName} {coach.LastName}",
                        Teams = teams
                    });

                    TempData["Error"] = "This coach has assigned teams. You must reassign them before setting this coach to inactive. <a href='#' data-bs-toggle='modal' data-bs-target='#reassignTeamsModal'>Reassign Teams</a>";

                    return RedirectToAction("ManageCoach");
                }
            }

            // SAFE TO UPDATE STATUS NOW
            coach.Status = model.Status;

            _context.SaveChanges();

            TempData["Success"] = "Coach updated successfully!";
            return RedirectToAction("ManageCoach");
        }



        [HttpPost]
        public IActionResult ReassignTeams(int oldCoachId, int newCoachId)
        {
            // Get all teams assigned to the old coach
            var teams = _context.Teams.Where(t => t.CoachId == oldCoachId).ToList();

            if (!teams.Any())
            {
                TempData["Error"] = "No teams found to reassign.";
                return RedirectToAction("ManageCoach");
            }

            // Reassign teams to the new coach
            foreach (var team in teams)
            {
                team.CoachId = newCoachId;
            }

            _context.SaveChanges();

            TempData["Success"] = "Teams reassigned successfully!";
            return RedirectToAction("ManageCoach");
        }


            [HttpPost]
            public IActionResult ChangeUserPassword(int id, string newPassword, string confirmPassword)
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                    return RedirectToAction("ManageCoach");

                if (newPassword != confirmPassword)
                {
                    TempData[$"PasswordError-{id}"] = "Passwords do not match!";
                    return RedirectToAction("ManageCoach");
                }

                // Hash the new password
                var passwordHasher = new PasswordHasher<User>();
                user.Password = passwordHasher.HashPassword(user, newPassword);

                _context.SaveChanges();

                TempData["PasswordSuccess"] = $"Password for {user.Username} updated successfully!";
                return RedirectToAction("ManageCoach");
            }


        [HttpPost]
        public IActionResult EditUserRole(int id, string role)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                // Check if the user is currently a Coach and the role is being changed
                bool wasCoach = user.Role == "coach";
                bool isChangingRole = role != "coach";

                if (wasCoach && isChangingRole)
                {
                    // Get teams assigned to this coach
                    var coach = _context.Coaches.FirstOrDefault(c => c.Username == user.Username);
                    if (coach != null)
                    {
                        var teams = _context.Teams
                            .Where(t => t.CoachId == coach.Id)
                            .Select(t => new { t.TeamId, t.TeamName })
                            .ToList();

                        if (teams.Any())
                        {
                            // Store data in TempData for reassign modal
                            TempData["ReassignTeams"] = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                CoachId = coach.Id,
                                CoachName = $"{coach.FirstName} {coach.LastName}",
                                Teams = teams
                            }, new System.Text.Json.JsonSerializerOptions
                            {
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });

                            TempData["Error"] = $"Cannot change role for {coach.FirstName} {coach.LastName} because they have assigned teams. <a href='#' data-bs-toggle='modal' data-bs-target='#reassignTeamsModal'>Reassign Teams</a>";

                            // Stop here, redirect to show modal
                            return RedirectToAction("ManageCoach", new { panel = "role" });
                        }
                    }
                }

                // Update role normally if no teams or not a coach
                user.Role = role;
                _context.SaveChanges();
            }

            return RedirectToAction("ManageCoach", new { panel = "role" });
        }



        [HttpPost]
        public IActionResult ToggleUserStatus(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);

            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageCoach");
            }

            // If user is a Coach and we are deactivating them
            if (user.Role == "coach" && user.Status == "Active")
            {
                // Check if coach has assigned teams
                var teams = _context.Teams
                    .Where(t => t.CoachId == user.Id)
                    .Select(t => new { t.TeamId, t.TeamName })
                    .ToList();

                if (teams.Any())
                {
                    // Prepare reassignment modal
                    TempData["ReassignTeams"] = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        CoachId = user.Id,
                        CoachName = $"{user.FirstName} {user.LastName}",
                        Teams = teams
                    }, new System.Text.Json.JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    TempData["Error"] =
                        $"Cannot deactivate {user.FirstName} {user.LastName} because they have assigned teams. " +
                        "<a href='#' data-bs-toggle='modal' data-bs-target='#reassignTeamsModal'>Reassign Teams</a>";

                    return RedirectToAction("ManageCoach");
                }
            }

            // If no teams or not a coach - just toggle status
            user.Status = user.Status == "Active" ? "Inactive" : "Active";
            _context.Users.Update(user);
            _context.SaveChanges();

            TempData["Success"] = $"User {user.FirstName} {user.LastName} is now {user.Status}.";
            return RedirectToAction("ManageCoach");
        }



        // POST: Add Coach
        [HttpPost]
        public IActionResult AddCoach(string firstName, string lastName, string username, string email, string password, string role, string Phone)
        {
            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(role))
            {
                TempData["ErrorMessage"] = "All required fields must be filled.";
                return RedirectToAction("ManageCoach");
            }

            // Check if username already exists
            if (_context.Users.Any(u => u.Username == username))
            {
                TempData["ErrorMessage"] = "Username already exists. Please choose a different username.";
                return RedirectToAction("ManageCoach");
            }

            var passwordHasher = new PasswordHasher<User>();

            // Prepare new User
            var newUser = new User
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Username = username.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Role = role.ToLower(), // ensure lowercase
                Status = "Active"
            };

            // Hash password
            newUser.Password = passwordHasher.HashPassword(newUser, password);

            // If Coach role is selected → also insert into Coaches table
            if (role.ToLower() == "coach")
            {
                var newCoach = new Coach
                {
                    Username = username.Trim(),
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Password = password, // for display (optional)
                    Phone = Phone?.Trim(),
                    Status = "Active",
                    ImageUrl = "~/assets/coach.jpg",
                    Bio = "Fill up if applicable"
                };

                _context.Coaches.Add(newCoach);
                _context.Users.Add(newUser);
            }
            else
            {
                _context.Users.Add(newUser);
            }


            _context.SaveChanges();

            // Show success in modal
            TempData["NewCoachUsername"] = username;
            TempData["NewCoachPassword"] = password;
            TempData["SuccessMessage"] = $"{role} added successfully!";

            return RedirectToAction("ManageCoach");
        }



        // POST: Delete Coach
        [HttpPost]
        public IActionResult DeleteCoach(int id)
        {
            var coach = _context.Coaches.Find(id);
            if (coach == null)
            {
                TempData["Error"] = "Coach not found.";
                return RedirectToAction("ManageCoach");
            }

            try
            {
                _context.Coaches.Remove(coach);
                _context.SaveChanges();
                TempData["Success"] = $"Coach {coach.FirstName} {coach.LastName} deleted successfully!";
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("foreign key constraint"))
                {
                    // Get teams assigned to this coach
                    var teams = _context.Teams
                        .Where(t => t.CoachId == coach.Id)
                        .Select(t => new { t.TeamId, t.TeamName })
                        .ToList();

                    if (teams.Any())
                    {
                        TempData["ReassignTeams"] = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            CoachId = coach.Id,
                            CoachName = $"{coach.FirstName} {coach.LastName}",
                            Teams = teams
                        }, new System.Text.Json.JsonSerializerOptions
                        {
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });

                        TempData["Error"] = $"Cannot delete {coach.FirstName} {coach.LastName} because they have assigned teams. <a href='#' data-bs-toggle='modal' data-bs-target='#reassignTeamsModal'>Reassign Teams</a>";
                    }
                    else
                    {
                        TempData["Error"] = "Coach has no teams but deletion failed due to DB constraints.";
                    }
                }
                else
                {
                    TempData["Error"] = "An error occurred while deleting the coach.";
                }
            }

            return RedirectToAction("ManageCoach");
        }



        // Password generator
        private string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // ---------------------- Competition Management ----------------------
        [HttpGet]
        public IActionResult ManageCompetition()
        {
            var competitions = _context.Competitions.ToList();
            return View(competitions);
        }

        // ✅ Add new competition to the database
        [HttpPost]
        public IActionResult AddCompetition(Competition newComp)
        {
            // Check for venue conflicts
            bool conflict = _context.Competitions
            .Any(c =>
                c.Venue.ToLower() == newComp.Venue.ToLower() &&
                ((newComp.StartDate >= c.StartDate && newComp.StartDate < c.EndDate) ||
                 (newComp.EndDate > c.StartDate && newComp.EndDate <= c.EndDate))
            );


            if (conflict)
            {
                TempData["ConflictMessage"] = "Conflict detected! Another competition is scheduled at this venue during this time.";
                return RedirectToAction("ManageCompetition");
            }

            _context.Competitions.Add(newComp);
            _context.SaveChanges();

            return RedirectToAction("ManageCompetition");
        }

        public IActionResult ManageTeamManager()
        {
            // Include Sport and Coach names for display
            var teams = _context.Teams
                .Include(t => t.Sport)
                .Include(t => t.Coach)
                .ToList();

            return View(teams);
        }


        [HttpPost]
        public IActionResult AcceptTeam(int id)
        {
            var team = _context.Teams.FirstOrDefault(t => t.TeamId == id);
            if (team != null)
            {
                team.Status = "Accepted";
                _context.SaveChanges();
            }
            return RedirectToAction("ManageTeamManager");
        }

        [HttpPost]
        public IActionResult DeclineTeam(int id)
        {
            var team = _context.Teams.FirstOrDefault(t => t.TeamId == id);
            if (team != null)
            {
                team.Status = "Declined";
                _context.SaveChanges();
            }
            return RedirectToAction("ManageTeamManager");
        }

        public async Task<IActionResult> AdminInterface()
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

        [HttpGet]
        public async Task<IActionResult> GetSports(int? teamId = null)
        {
            // If teamId is provided, filter sports that are assigned to that team
            if (teamId.HasValue)
            {
                var sports = await _context.Teams
                    .Where(t => t.TeamId == teamId.Value && t.Status == "Accepted")
                    .Join(_context.Sports,
                          t => t.SportId,
                          s => s.Id,
                          (t, s) => new
                          {
                              sportId = s.Id,
                              sportName = s.Name
                          })
                    .Distinct()
                    .ToListAsync();

                return Json(sports);
            }
            else
            {
                // Return all sports if no team is selected
                var sports = await _context.Sports
                    .Select(s => new
                    {
                        sportId = s.Id,
                        sportName = s.Name
                    })
                    .ToListAsync();

                return Json(sports);
            }
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


        [HttpGet("Admin/GetAthletes/{teamId}")]
        public IActionResult GetAthletes(int teamId)
        {
            var athletes = _context.Athletes
                .Where(a => a.TeamId == teamId)
                .Select(a => new {
                    a.AthleteId,
                    a.FirstName,
                    a.LastName,
                    a.Gender,
                    Birthdate = a.Birthdate.HasValue ? a.Birthdate.Value.ToString("yyyy-MM-dd") : null,
                    a.MedicalInfo,
                    a.Grades,
                    a.Status
                })
                .ToList();

            return Json(athletes);
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

        [HttpPost]
        public IActionResult UpdateUser(int id, string firstName, string lastName, string? email, string role, string status, string? newPassword, string? confirmPassword)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageCoach", new { panel = "role" });
            }

            // Validate names
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                TempData["Error"] = "First and last name are required.";
                return RedirectToAction("ManageCoach", new { panel = "role" });
            }

            // If changing from Coach to another role, ensure no assigned teams
            if (user.Role == "coach" && role != "coach")
            {
                var coach = _context.Coaches.FirstOrDefault(c => c.Username == user.Username);
                if (coach != null)
                {
                    var teams = _context.Teams.Where(t => t.CoachId == coach.Id).Select(t => new { t.TeamId, t.TeamName }).ToList();
                    if (teams.Any())
                    {
                        TempData["ReassignTeams"] = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            CoachId = coach.Id,
                            CoachName = $"{coach.FirstName} {coach.LastName}",
                            Teams = teams
                        }, new System.Text.Json.JsonSerializerOptions
                        {
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });
                        TempData["Error"] = "Cannot change role while coach has assigned teams. Reassign first.";
                        return RedirectToAction("ManageCoach", new { panel = "role" });
                    }
                }
            }

            // Update core fields
            user.FirstName = firstName.Trim();
            user.LastName = lastName.Trim();
            user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            user.Role = role;
            user.Status = status;

            // Optional password change
            if (!string.IsNullOrWhiteSpace(newPassword) || !string.IsNullOrWhiteSpace(confirmPassword))
            {
                if (newPassword != confirmPassword)
                {
                    TempData[$"PasswordError-{id}"] = "Passwords do not match!";
                    return RedirectToAction("ManageCoach", new { panel = "role" });
                }
                var passwordHasher = new PasswordHasher<User>();
                user.Password = passwordHasher.HashPassword(user, newPassword!);
            }

            _context.SaveChanges();
            TempData["Success"] = "User updated.";
            return RedirectToAction("ManageCoach", new { panel = "role" });
        }
    }
}
