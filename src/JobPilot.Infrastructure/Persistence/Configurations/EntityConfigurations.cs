using JobPilot.Domain.Companies;
using JobPilot.Domain.JobApplications;
using JobPilot.Domain.Resumes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobPilot.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : EntityConfiguration<Company, CompanyId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(Company.NameMaxLength).IsRequired();
        builder.Property(c => c.Website).HasMaxLength(2048);
        builder.Property(c => c.Industry).HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class JobApplicationConfiguration : EntityConfiguration<JobApplication, JobApplicationId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<JobApplication> builder)
    {
        builder.Property(a => a.Title).HasMaxLength(JobApplication.TitleMaxLength).IsRequired();
        builder.Property(a => a.PostingUrl).HasMaxLength(2048);
        builder.Property(a => a.WorkMode).HasConversion<string>().HasMaxLength(16);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.UpdatedAt);

        // Value object -> columnas en la misma tabla.
        builder.OwnsOne(a => a.Salary, salary =>
        {
            salary.Property(s => s.Min).HasColumnName("SalaryMin").HasPrecision(12, 2);
            salary.Property(s => s.Max).HasColumnName("SalaryMax").HasPrecision(12, 2);
            salary.Property(s => s.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
        });

        builder.Property(a => a.Analysis)
            .HasConversion<JsonValueConverter<JobPostingAnalysis>>()
            .HasColumnType("jsonb");

        builder.Property(a => a.Embedding)
            .HasConversion<FloatArrayToVectorConverter, FloatArrayComparer>()
            .HasColumnType("vector");

        builder.OwnsMany(a => a.StatusHistory, history =>
        {
            history.ToTable("JobApplicationStatusHistory");
            history.WithOwner().HasForeignKey("JobApplicationId");
            history.Property<int>("Id");
            history.HasKey("Id");
        });

        builder.HasMany(a => a.Interviews)
            .WithOne()
            .HasForeignKey("JobApplicationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // EF escribe directamente en los campos privados _statusHistory / _interviews.
        builder.Navigation(a => a.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Interviews).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InterviewConfiguration : EntityConfiguration<Interview, InterviewId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Interview> builder)
    {
        builder.ToTable("Interviews");
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(i => i.Notes).HasMaxLength(2000);
    }
}

internal sealed class ResumeConfiguration : EntityConfiguration<Resume, ResumeId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Resume> builder)
    {
        builder.Property(r => r.Name).HasMaxLength(Resume.NameMaxLength).IsRequired();
        builder.Property(r => r.Content).IsRequired();

        builder.Property(r => r.Embedding)
            .HasConversion<FloatArrayToVectorConverter, FloatArrayComparer>()
            .HasColumnType("vector");
    }
}
