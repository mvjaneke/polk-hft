using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POLK_DOTNET.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryAlbums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the GalleryAlbums table first
            migrationBuilder.CreateTable(
                name: "GalleryAlbums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    CoverImageFileName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryAlbums", x => x.Id);
                });

            // 2. Insert a default album for any existing orphan images
            migrationBuilder.Sql(@"
                INSERT INTO GalleryAlbums (Title, Description, Category, CoverImageFileName, CreatedAt)
                SELECT 'General', 'Migrated images', 'social',
                    (SELECT FileName FROM GalleryImages LIMIT 1),
                    datetime('now')
                WHERE EXISTS (SELECT 1 FROM GalleryImages);
            ");

            // 3. Add the FK column with default pointing to the newly created album
            migrationBuilder.AddColumn<int>(
                name: "GalleryAlbumId",
                table: "GalleryImages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // 4. Point existing images to the default album
            migrationBuilder.Sql(@"
                UPDATE GalleryImages
                SET GalleryAlbumId = (SELECT Id FROM GalleryAlbums WHERE Title = 'General' LIMIT 1)
                WHERE GalleryAlbumId = 0
                  AND EXISTS (SELECT 1 FROM GalleryAlbums WHERE Title = 'General');
            ");

            // 5. Create index and FK constraint
            migrationBuilder.CreateIndex(
                name: "IX_GalleryImages_GalleryAlbumId",
                table: "GalleryImages",
                column: "GalleryAlbumId");

            migrationBuilder.AddForeignKey(
                name: "FK_GalleryImages_GalleryAlbums_GalleryAlbumId",
                table: "GalleryImages",
                column: "GalleryAlbumId",
                principalTable: "GalleryAlbums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GalleryImages_GalleryAlbums_GalleryAlbumId",
                table: "GalleryImages");

            migrationBuilder.DropTable(
                name: "GalleryAlbums");

            migrationBuilder.DropIndex(
                name: "IX_GalleryImages_GalleryAlbumId",
                table: "GalleryImages");

            migrationBuilder.DropColumn(
                name: "GalleryAlbumId",
                table: "GalleryImages");
        }
    }
}
