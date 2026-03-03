using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using UniversityManagementSystem.Core.Application.AI.Contracts;
using UniversityManagementSystem.Core.DTOs;
using UniversityManagementSystem.Core.Entities;
using UniversityManagementSystem.Core.Interfaces;

namespace UniversityManagementSystem.Core.Application.AI.Tools;

public class CreateGeneratedExamTool : IAiTool
{
    private readonly IExamService _examService;

    public CreateGeneratedExamTool(IExamService examService)
    {
        _examService = examService;
    }

    public string Name => "CreateGeneratedExam";

    public async Task<object> ExecuteAsync(object parameters, ClaimsPrincipal user)
    {
        var paramJson = JsonSerializer.Serialize(parameters);
        var paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramJson)
            ?? throw new ArgumentException("Invalid parameters format");

        int subjectOfferingId = paramDict["subjectOfferingId"].GetInt32();
        string title = paramDict["title"].GetString() ?? "Generated Exam";

        var questionsList = new List<CreateExamQuestionDto>();
        if (paramDict.TryGetValue("questions", out var questionsElement) && questionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var q in questionsElement.EnumerateArray())
            {
                questionsList.Add(new CreateExamQuestionDto
                {
                    QuestionText = q.TryGetProperty("questionText", out var qt) ? qt.GetString() ?? string.Empty : string.Empty,
                    CorrectAnswer = q.TryGetProperty("correctAnswer", out var ca) ? ca.GetString() ?? string.Empty : string.Empty,
                    Mark = q.TryGetProperty("mark", out var markProp) && markProp.ValueKind == JsonValueKind.Number ? markProp.GetInt32() : 1
                });
            }
        }

        var nameIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("nameid")?.Value;

        if (string.IsNullOrEmpty(nameIdClaim) || !int.TryParse(nameIdClaim, out int doctorId))
            throw new UnauthorizedAccessException("Doctor ID not found in token.");

        var dto = new CreateExamDto
        {
            Title = title,
            Type = ExamType.Final,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            IsPublished = true,
            Questions = questionsList
        };

        var exam = await _examService.CreateExamAsync(subjectOfferingId, dto, doctorId);

        return new
        {
            examId = exam.Id,
            totalQuestions = exam.Questions.Count
        };
    }
}
