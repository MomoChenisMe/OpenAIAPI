﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using OpenAIAPI.Models;

namespace OpenAIAPI.Models.Configurations
{
    public partial class TB_EmbeddingsConfiguration : IEntityTypeConfiguration<TB_Embeddings>
    {
        public void Configure(EntityTypeBuilder<TB_Embeddings> entity)
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");

            entity.Property(e => e.Vector).IsRequired();

            OnConfigurePartial(entity);
        }

        partial void OnConfigurePartial(EntityTypeBuilder<TB_Embeddings> entity);
    }
}
