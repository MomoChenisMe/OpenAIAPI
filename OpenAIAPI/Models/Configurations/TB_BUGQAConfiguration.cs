﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenAIAPI.Models.Configurations
{
    public partial class TB_BUGQAConfiguration : IEntityTypeConfiguration<TB_BUGQA>
    {
        public void Configure(EntityTypeBuilder<TB_BUGQA> entity)
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");

            entity.Property(e => e.Bugano)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.TextHtml).IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasDefaultValueSql("(getdate())");

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<TB_BUGQA> entity);
    }
}
