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
            builder.Property(bg => bg.Code).IsRequired().HasMaxLength(3).IsUnicode(false);
            builder.HasIndex(bg => bg.Code).IsUnique();
            builder.HasData(
                new BloodGroup { Id = 1, Code = "A+" },
                new BloodGroup { Id = 2, Code = "A-" },
                new BloodGroup { Id = 3, Code = "B+" },
                new BloodGroup { Id = 4, Code = "B-" },
                new BloodGroup { Id = 5, Code = "AB+" },
                new BloodGroup { Id = 6, Code = "AB-" },
                new BloodGroup { Id = 7, Code = "O+" },
                new BloodGroup { Id = 8, Code = "O-" }
            );

        }
    }
}
