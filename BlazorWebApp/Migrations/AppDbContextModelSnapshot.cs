﻿// <auto-generated />
using System;
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.11");

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Folder", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Folders");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Image", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<float>("CfgScale")
                        .HasColumnType("REAL");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("TEXT");

                    b.Property<double?>("DenoisingStrength")
                        .HasColumnType("REAL");

                    b.Property<bool>("Favorite")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Info")
                        .HasColumnType("TEXT");

                    b.Property<string>("InfoPath")
                        .HasColumnType("TEXT");

                    b.Property<int>("ModeId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("NegativePrompt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("ProjectId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Prompt")
                        .HasColumnType("TEXT");

                    b.Property<int>("SamplerId")
                        .HasColumnType("INTEGER");

                    b.Property<long>("Seed")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Steps")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ModeId");

                    b.HasIndex("ProjectId");

                    b.ToTable("Images");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Mode", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Modes");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Project", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreationTime")
                        .HasColumnType("TEXT");

                    b.Property<int?>("FolderId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("SampleImagePath")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("FolderId");

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Prompt", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ImagePath")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsFavorite")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Negative")
                        .HasColumnType("TEXT");

                    b.Property<string>("Positive")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Prompts");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Resource", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Author")
                        .HasColumnType("TEXT");

                    b.Property<int?>("CivitaiModelId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("CivitaiModelVersionId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<string>("Filename")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("SubTypeId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Tags")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("TriggerWords")
                        .HasColumnType("TEXT");

                    b.Property<int>("TypeId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Filename")
                        .IsUnique();

                    b.HasIndex("SubTypeId");

                    b.HasIndex("TypeId");

                    b.ToTable("Resources");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.ResourceImage", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<float?>("CfgScale")
                        .HasColumnType("REAL");

                    b.Property<int>("CivitaiModelId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("CivitaiModelVersionID")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClipSkip")
                        .HasColumnType("TEXT");

                    b.Property<string>("DenoisingStrength")
                        .HasColumnType("TEXT");

                    b.Property<string>("ENSD")
                        .HasColumnType("TEXT");

                    b.Property<string>("FaceRestoration")
                        .HasColumnType("TEXT");

                    b.Property<string>("GenerationProcess")
                        .HasColumnType("TEXT");

                    b.Property<string>("Hash")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int?>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<string>("HiresSteps")
                        .HasColumnType("TEXT");

                    b.Property<string>("HiresUpscale")
                        .HasColumnType("TEXT");

                    b.Property<string>("HiresUpscaler")
                        .HasColumnType("TEXT");

                    b.Property<string>("Model")
                        .HasColumnType("TEXT");

                    b.Property<string>("ModelHash")
                        .HasColumnType("TEXT");

                    b.Property<string>("NegativePrompt")
                        .HasColumnType("TEXT");

                    b.Property<bool?>("Nsfw")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Prompt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Sampler")
                        .HasColumnType("TEXT");

                    b.Property<long?>("Seed")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("Steps")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Tags")
                        .HasColumnType("TEXT");

                    b.Property<string>("Url")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Hash")
                        .IsUnique();

                    b.ToTable("ResourceImages");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.ResourceSubType", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("ResourceSubTypes");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.ResourceType", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("ResourceTypes");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Sampler", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Samplers");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Image", b =>
                {
                    b.HasOne("BlazorWebApp.Data.Entities.Mode", null)
                        .WithMany("Images")
                        .HasForeignKey("ModeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("BlazorWebApp.Data.Entities.Project", null)
                        .WithMany("Images")
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Project", b =>
                {
                    b.HasOne("BlazorWebApp.Data.Entities.Folder", "Folder")
                        .WithMany("Projects")
                        .HasForeignKey("FolderId");

                    b.Navigation("Folder");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Resource", b =>
                {
                    b.HasOne("BlazorWebApp.Data.Entities.ResourceSubType", "SubType")
                        .WithMany()
                        .HasForeignKey("SubTypeId");

                    b.HasOne("BlazorWebApp.Data.Entities.ResourceType", "Type")
                        .WithMany()
                        .HasForeignKey("TypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("SubType");

                    b.Navigation("Type");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Folder", b =>
                {
                    b.Navigation("Projects");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Mode", b =>
                {
                    b.Navigation("Images");
                });

            modelBuilder.Entity("BlazorWebApp.Data.Entities.Project", b =>
                {
                    b.Navigation("Images");
                });
#pragma warning restore 612, 618
        }
    }
}
