using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrinVideoEncoder.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "VideoFiles",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "TEXT", nullable: false),
				DirectoryPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
				DurationSeconds = table.Column<long>(type: "INTEGER", nullable: true),
				Filename = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
				FileSizeCompressed = table.Column<long>(type: "INTEGER", nullable: true),
				FileSizeOriginal = table.Column<long>(type: "INTEGER", nullable: false),
				Fps = table.Column<double>(type: "REAL", nullable: true),
				Height = table.Column<int>(type: "INTEGER", nullable: true),
				IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
				LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
				Status = table.Column<int>(type: "INTEGER", nullable: false),
				Width = table.Column<int>(type: "INTEGER", nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_VideoFiles", x => x.Id);
			});
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(
			name: "VideoFiles");
	}
}
