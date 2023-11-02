﻿// <auto-generated />
using System;
using Mewdeko.GlobalBanAPI.DbStuff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Mewdeko.GlobalBanAPI.Migrations
{
    [DbContext(typeof(ApiContext))]
    [Migration("20230930192407_MoreStuff")]
    partial class MoreStuff
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.11");

            modelBuilder.Entity("Mewdeko.GlobalBanAPI.DbStuff.Models.GlobalBans", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("AddedBy")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ApprovedBy")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsAppealable")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsApproved")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Proof")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("RecommendedAction")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("GlobalBans");
                });
#pragma warning restore 612, 618
        }
    }
}
