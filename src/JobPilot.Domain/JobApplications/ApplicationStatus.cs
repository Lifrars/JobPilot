using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.JobApplications;

/// <summary>
/// Patrón State + smart enum. Cada estado es una subclase que decide a qué estados puede pasar,
/// así la regla "de Offer no se vuelve a Applied" vive en un solo lugar y no en if/else repartidos.
/// </summary>
public abstract class ApplicationStatus : Enumeration<ApplicationStatus>
{
    public static readonly ApplicationStatus Wishlist = new WishlistState();
    public static readonly ApplicationStatus Applied = new AppliedState();
    public static readonly ApplicationStatus Screening = new ScreeningState();
    public static readonly ApplicationStatus Interviewing = new InterviewingState();
    public static readonly ApplicationStatus Offer = new OfferState();
    public static readonly ApplicationStatus Accepted = new TerminalState(6, nameof(Accepted));
    public static readonly ApplicationStatus Rejected = new TerminalState(7, nameof(Rejected));
    public static readonly ApplicationStatus Withdrawn = new TerminalState(8, nameof(Withdrawn));

    private ApplicationStatus(int id, string name) : base(id, name) { }

    public bool IsTerminal => !NextStates().Any();

    /// <summary>¿Se pueden agendar entrevistas en este estado?</summary>
    public virtual bool AllowsInterviews => false;

    public bool CanTransitionTo(ApplicationStatus next) => NextStates().Contains(next);

    public IReadOnlyList<ApplicationStatus> AllowedTransitions => [.. NextStates()];

    protected abstract IEnumerable<ApplicationStatus> NextStates();

    private sealed class WishlistState() : ApplicationStatus(1, nameof(Wishlist))
    {
        protected override IEnumerable<ApplicationStatus> NextStates() => [Applied, Withdrawn];
    }

    private sealed class AppliedState() : ApplicationStatus(2, nameof(Applied))
    {
        public override bool AllowsInterviews => true;

        protected override IEnumerable<ApplicationStatus> NextStates() => [Screening, Interviewing, Rejected, Withdrawn];
    }

    private sealed class ScreeningState() : ApplicationStatus(3, nameof(Screening))
    {
        public override bool AllowsInterviews => true;

        protected override IEnumerable<ApplicationStatus> NextStates() => [Interviewing, Rejected, Withdrawn];
    }

    private sealed class InterviewingState() : ApplicationStatus(4, nameof(Interviewing))
    {
        public override bool AllowsInterviews => true;

        protected override IEnumerable<ApplicationStatus> NextStates() => [Offer, Rejected, Withdrawn];
    }

    private sealed class OfferState() : ApplicationStatus(5, nameof(Offer))
    {
        protected override IEnumerable<ApplicationStatus> NextStates() => [Accepted, Rejected, Withdrawn];
    }

    private sealed class TerminalState(int id, string name) : ApplicationStatus(id, name)
    {
        protected override IEnumerable<ApplicationStatus> NextStates() => [];
    }
}
