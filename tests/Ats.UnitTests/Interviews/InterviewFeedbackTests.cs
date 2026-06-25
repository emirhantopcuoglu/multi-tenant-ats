using Ats.Modules.Interviews.Domain;

namespace Ats.UnitTests.Interviews;

public class InterviewFeedbackTests
{
    private static readonly Guid ValidInterviewId = Guid.NewGuid();
    private static readonly Guid ValidInterviewerUserId = Guid.NewGuid();

    [Fact]
    public void Submit_should_create_feedback_with_correct_properties()
    {
        var feedback = InterviewFeedback.Submit(
            ValidInterviewId, ValidInterviewerUserId,
            rating: 4, FeedbackRecommendation.Hire, comments: "Good communication.");

        Assert.Equal(ValidInterviewId, feedback.InterviewId);
        Assert.Equal(ValidInterviewerUserId, feedback.InterviewerUserId);
        Assert.Equal(4, feedback.Rating);
        Assert.Equal(FeedbackRecommendation.Hire, feedback.Recommendation);
        Assert.Equal("Good communication.", feedback.Comments);
        Assert.NotEqual(Guid.Empty, feedback.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Submit_should_throw_when_rating_is_out_of_range(int invalidRating)
    {
        var act = () => InterviewFeedback.Submit(
            ValidInterviewId, ValidInterviewerUserId,
            invalidRating, FeedbackRecommendation.NoHire, null);

        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Submit_should_throw_when_interview_id_is_empty()
    {
        var act = () => InterviewFeedback.Submit(
            Guid.Empty, ValidInterviewerUserId, 3, FeedbackRecommendation.Hire, null);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Submit_should_throw_when_interviewer_user_id_is_empty()
    {
        var act = () => InterviewFeedback.Submit(
            ValidInterviewId, Guid.Empty, 3, FeedbackRecommendation.Hire, null);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Submit_should_trim_and_null_empty_comments()
    {
        var feedback = InterviewFeedback.Submit(
            ValidInterviewId, ValidInterviewerUserId, 3, FeedbackRecommendation.Hire, "  ");

        Assert.Null(feedback.Comments);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Submit_should_accept_boundary_ratings(int rating)
    {
        var feedback = InterviewFeedback.Submit(
            ValidInterviewId, ValidInterviewerUserId, rating, FeedbackRecommendation.StrongHire, null);

        Assert.Equal(rating, feedback.Rating);
    }
}
