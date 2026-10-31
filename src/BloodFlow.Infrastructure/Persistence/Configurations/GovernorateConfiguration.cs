using BloodFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodFlow.Infrastructure.Persistence.Configurations
{
    internal class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
    {
        public void Configure(EntityTypeBuilder<Governorate> builder)
        {
            builder.HasKey(g => g.Id);

            builder
                .Property(g => g.Id)
                .ValueGeneratedNever();

            builder
                .Property(g => g.Name)
                .IsRequired()
                .HasMaxLength(60);

            builder
                .HasIndex(g => g.Name)
                .IsUnique();

            builder.HasData(
                new Governorate { Id = 1, Name = "القاهرة" },
                new Governorate { Id = 2, Name = "الإسكندرية" },
                new Governorate { Id = 3, Name = "بورسعيد" },
                new Governorate { Id = 4, Name = "السويس" },
                new Governorate { Id = 5, Name = "البحر الأحمر" },
                new Governorate { Id = 6, Name = "دمياط" },
                new Governorate { Id = 7, Name = "الدقهلية" },
                new Governorate { Id = 8, Name = "الشرقية" },
                new Governorate { Id = 9, Name = "الإسماعيلية" },
                new Governorate { Id = 10, Name = "الجيزة" },
                new Governorate { Id = 11, Name = "المنوفية" },
                new Governorate { Id = 12, Name = "القليوبية" },
                new Governorate { Id = 13, Name = "البحيرة" },
                new Governorate { Id = 14, Name = "الغربية" },
                new Governorate { Id = 15, Name = "الأقصر" },
                new Governorate { Id = 16, Name = "أسيوط" },
                new Governorate { Id = 17, Name = "بني سويف" },
                new Governorate { Id = 18, Name = "الفيوم" },
                new Governorate { Id = 19, Name = "المنيا" },
                new Governorate { Id = 20, Name = "سوهاج" },
                new Governorate { Id = 21, Name = "قنا" },
                new Governorate { Id = 22, Name = "كفر الشيخ" },
                new Governorate { Id = 23, Name = "مطروح" },
                new Governorate { Id = 24, Name = "أسوان" },
                new Governorate { Id = 25, Name = "شمال سيناء" },
                new Governorate { Id = 26, Name = "جنوب سيناء" },
                new Governorate { Id = 27, Name = "الوادي الجديد" }
            );
        }
    }
}
