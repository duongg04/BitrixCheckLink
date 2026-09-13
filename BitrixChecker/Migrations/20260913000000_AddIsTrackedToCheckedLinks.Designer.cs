using System;
using BitrixChecker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace BitrixChecker.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260913000000_AddIsTrackedToCheckedLinks")]
    partial class AddIsTrackedToCheckedLinks
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            // Designer file for this migration (EF handles the snapshot)
#pragma warning restore 612, 618
        }
    }
}
