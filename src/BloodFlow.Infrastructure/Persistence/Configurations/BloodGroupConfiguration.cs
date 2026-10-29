using System;
using BloodFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class BloodGroupConfiguration : IEntityTypeConfiguration<BloodGroup>
    {
        public void Configure(EntityTypeBuilder<BloodGroup> builder)
        {
            builder.HasKey(bg => bg.Id);
            builder.Property(bg => bg.Id).ValueGeneratedNever();
            builder.Property(bg => bg.Code).IsRequired().HasMaxLength(3);
           builder.HasIndex(bg => bg.Code).IsUnique();
           
        }
    }
}
