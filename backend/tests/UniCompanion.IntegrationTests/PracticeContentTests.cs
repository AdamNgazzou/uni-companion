using System.Text.Json;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Practice;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class PracticeContentTests
{
    private static readonly StudySource Source = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "lecture.pdf", 2, "A map operation transforms each input into intermediate key value pairs.");
    private static object TaskData(string kind = "written", Guid? sourceId = null) => new
    {
        kind,
        title = "Synthetic word count",
        prompt = "Explain the intermediate mapping for the supplied generated words.",
        given = "Synthetic input: cat dog cat",
        hints = new[] { "Read each token." },
        solution = "The mapping emits (cat,1), (dog,1), (cat,1). [" + Source.ChunkId + "]",
        rubric = new[] { "One intermediate pair per word" },
        citations = new[] { sourceId ?? Source.ChunkId },
        starterCode = kind == "coding" ? "PRIVATE_SOLUTION_LEAK_BY_MODEL" : null,
        solutionCode = kind == "coding" ? "def solve(data):\n    return {'cat': 2, 'dog': 1}" : null,
        runInstructions = kind == "coding" ? "Save as lab.py; python3 lab.py" : null,
        inputJson = kind == "coding" ? "[\"cat\",\"dog\",\"cat\"]" : null,
        expectedOutput = kind == "coding" ? "{\"cat\":2,\"dog\":1}" : null,
        steps = kind == "coding" ? new[] { "Tokenize", "Map", "Reduce" } : [],
        checks = kind == "coding" ? new[] { new { input = "[\"cat\",\"dog\",\"cat\"]", expected = "{\"cat\":2,\"dog\":1}" }, new { input = "[]", expected = "{}" } } : []
    };
    [Fact]
    public void ExistingStudyRequestShapeIsPreservedWhenPracticeIsAbsent()
    {
        var request = JsonSerializer.Serialize(new StudyRequest("summary", MaterialId: Guid.Empty), UniCompanion.Infrastructure.Study.StudyJobs.Json);
        Assert.DoesNotContain("practice", request);
    }
    [Fact]
    public void MixedPracticeKeepsPrivateSolutionsAndRealVersionedCitations()
    {
        var json = JsonSerializer.Serialize(new { objectives = new[] { "Apply map/reduce" }, prerequisites = Array.Empty<string>(), tasks = new[] { TaskData(), TaskData(), TaskData("coding") } });
        var artifact = PracticeComposer.Parse(json, "mixed", "en", [Source]);
        Assert.Equal(3, artifact.Tasks.Length); Assert.Equal(3, artifact.Tasks.Select(t => t.Id).Distinct().Count());
        Assert.Equal(Source.VersionId, artifact.Tasks[0].Citations[0].VersionId);
        Assert.Equal(Source.Text, artifact.Tasks[0].Citations[0].Quote);
        Assert.DoesNotContain(Source.ChunkId.ToString(), artifact.Tasks[0].Solution);
        Assert.NotNull(artifact.Tasks[2].SolutionCode);
        Assert.DoesNotContain("return", artifact.Tasks[2].StarterCode);
        Assert.DoesNotContain("PRIVATE_SOLUTION_LEAK_BY_MODEL", artifact.Tasks[2].StarterCode);
        Assert.Contains("NotImplementedError", artifact.Tasks[2].StarterCode);
    }
    [Fact]
    public void WrongTaskCountsAndUnknownSourceIdsAreRejected()
    {
        var wrongCount = JsonSerializer.Serialize(new { objectives = new[] { "Apply map/reduce" }, prerequisites = Array.Empty<string>(), tasks = new[] { TaskData() } });
        Assert.Throws<JsonException>(() => PracticeComposer.Parse(wrongCount, "mixed", "en", [Source]));
        var unknownSource = JsonSerializer.Serialize(new { objectives = new[] { "Apply map/reduce" }, prerequisites = Array.Empty<string>(), tasks = new[] { TaskData(sourceId: Guid.NewGuid()), TaskData(), TaskData() } });
        Assert.Throws<JsonException>(() => PracticeComposer.Parse(unknownSource, "written", "en", [Source]));
    }
}
