#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public class SessionFeedbackTests
{
    private static DeviceContextDto SavedResult()
    {
        return new DeviceContextDto
        {
            recommendation = new RecommendationDto { value = "repeat", clinicalStatus = "pendiente_de_validar" },
            lastFeedback = new DeviceSessionFeedbackDto
            {
                sessionId = "saved-session",
                levelReached = 2,
                totalDurationSeconds = 180,
                analysis = new DeviceAnalysisDto { explanation = "Explicación de las reglas.", narrativeStatus = "disabled" }
            }
        };
    }

    [Test]
    public void RequestedSessionDoesNotDisplayAnOlderResult()
    {
        string text = SavedResult().FeedbackText("new-session");
        StringAssert.DoesNotContain("Nivel registrado: 2", text);
        StringAssert.Contains("todavía no está disponible", text);
    }

    [Test]
    public void SavedResultIncludesRecordedMetricsAndDeterministicExplanation()
    {
        string text = SavedResult().FeedbackText("saved-session");
        StringAssert.Contains("180 segundos", text);
        StringAssert.Contains("Explicación de las reglas.", text);
        StringAssert.Contains("No hay una sesión anterior", text);
    }

    [Test]
    public void PendingNarrativeRetainsTheAvailableExplanation()
    {
        DeviceContextDto context = SavedResult();
        context.lastFeedback.analysis.narrativeStatus = "pending";
        context.lastFeedback.analysis.narrative = "Must not be displayed yet";
        string text = context.FeedbackText();
        StringAssert.Contains("Explicación de las reglas.", text);
        StringAssert.DoesNotContain("Must not be displayed yet", text);
    }
}
#endif
