using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;

namespace TaskManager.Infrastructure.Persistence.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public const int MaxStatusLength = 16;

    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(task => task.Id);

        builder.Property(task => task.Title)
            .HasMaxLength(TaskItem.MaxTitleLength)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasMaxLength(TaskItem.MaxDescriptionLength);

        builder.Property(task => task.Status)
            .HasConversion<string>()
            .HasMaxLength(MaxStatusLength)
            .IsRequired();

        builder.Property(task => task.DueDate)
            .HasColumnType("date");

        builder.Property(task => task.CreatedAt).IsRequired();

        builder.Property(task => task.OwnerId).IsRequired();

        builder.HasIndex(task => task.OwnerId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(task => task.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
