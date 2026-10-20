using DWCCSportsManagement.Models;
using System.Collections.Generic;

public class CompetitionParticipationViewModel
{
    public List<Competition> Competitions { get; set; }
    public List<TeamModel> Teams { get; set; }

    public List<CompetitionParticipant> Participants { get; set; }
}
