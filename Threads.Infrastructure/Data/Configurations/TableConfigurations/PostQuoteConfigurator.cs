using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class PostQuoteConfigurator : IEntityTypeConfiguration<PostQuote>
{
    public void Configure(EntityTypeBuilder<PostQuote> builder)
    {
        builder.ToTable("PostQuotes");

        builder.HasKey(quote => quote.PostId);

        builder.Property(quote => quote.TargetType)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(quote => quote.TargetId)
            .IsRequired();

        builder.Property(quote => quote.TargetVersionId)
            .IsRequired();

        builder.Property(quote => quote.CreatedAt)
            .IsRequired();

        builder.HasIndex(quote => new
        {
            quote.TargetType,
            quote.TargetId
        });
        
        builder.HasOne(quote => quote.Post)
            .WithOne(post => post.Quote)
            .HasForeignKey<PostQuote>(quote => quote.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_PostQuotes_TargetType",
                "\"TargetType\" IN ('Post', 'Comment')");

            table.HasCheckConstraint(
                "CK_PostQuotes_TargetId",
                "\"TargetId\" <> '00000000-0000-0000-0000-000000000000'");

            table.HasCheckConstraint(
                "CK_PostQuotes_TargetVersionId",
                "\"TargetVersionId\" <> '00000000-0000-0000-0000-000000000000'");
        });
    }
}
