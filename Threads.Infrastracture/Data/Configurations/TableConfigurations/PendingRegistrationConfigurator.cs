using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class PendingRegistrationConfigurator : IEntityTypeConfiguration<PendingRegistration>
{
    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.ToTable("PendingRegistrations");

        builder.HasKey(registration => registration.Id);

        builder.Property(registration => registration.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(registration => registration.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(registration => registration.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(registration => registration.VerificationCode)
            .IsRequired()
            .HasMaxLength(6);

        builder.Property(registration => registration.DisplayName)
            .HasMaxLength(100);

        builder.Property(registration => registration.CreatedAt)
            .IsRequired();

        builder.HasIndex(registration => registration.Email)
            .IsUnique();

        builder.HasIndex(registration => registration.Username)
            .IsUnique();
    }
}
