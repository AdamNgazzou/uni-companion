using UniCompanion.Core.Study;

namespace UniCompanion.Core.Practice;

public sealed record PracticeOptions(string Mode = "mixed", string Difficulty = "guided", int Minutes = 30);
public sealed record PracticeCheck(string Input, string Expected);
public sealed record PracticeTask(string Id, string Kind, string Title, string Prompt, string Given,
    string[] Hints, string Solution, string[] Rubric, StudyCitation[] Citations,
    string? StarterCode, string? SolutionCode, string? RunInstructions, string? ExpectedOutput, string[] Steps, PracticeCheck[] Checks);
public sealed record PracticeArtifact(string Mode, string Language, string[] Objectives, string[] Prerequisites, PracticeTask[] Tasks);
