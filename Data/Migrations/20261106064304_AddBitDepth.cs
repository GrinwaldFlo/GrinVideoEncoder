using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrinVideoEncoder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBitDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BitDepth",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BitDepth",
                table: "VideoFiles");
        }
    }
}
