using System;
using BloodFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
internal class GovernorateConfiguration:IEntityTypeConfiguration<Governorate>
    {
        public void Configure(EntityTypeBuilder<Governorate> builder)
        {
           
            builder.HasKey(g => g.Id);
            builder.Property(g => g.Id).ValueGeneratedNever();
            builder.Property(g => g.Name).IsRequired().HasMaxLength(60);
            builder.HasIndex(g => g.Name).IsUnique();


        }


    }
}
