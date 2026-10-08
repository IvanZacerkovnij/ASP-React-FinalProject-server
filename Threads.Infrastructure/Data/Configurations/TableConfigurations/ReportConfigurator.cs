using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public sealed class ReportConfigurator : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.TargetType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(report => report.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(report => report.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(report => report.Decision).HasConversion<string>().HasMaxLength(16);
        builder.Property(report => report.Reason).HasMaxLength(32).IsRequired();
        builder.Property(report => report.SystemCode).HasMaxLength(100);
        builder.Property(report => report.SystemLabel).HasMaxLength(255);
        builder.Property(report => report.CreatedAt).IsRequired();

        builder.HasIndex(report => report.Status);
        builder.HasIndex(report => report.Source);
        builder.HasIndex(report => report.Decision);
        builder.HasIndex(report => report.CreatedAt);
        builder.HasIndex(report => report.ReporterId);
        builder.HasIndex(report => new { report.TargetType, report.TargetId });

        builder.HasOne(report => report.Reporter)
            .WithMany(user => user.SubmittedReports)
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.ResolvedBy)
            .WithMany(user => user.ResolvedReports)
            .HasForeignKey(report => report.ResolvedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
