using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Community : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    IsSample = table.Column<bool>(type: "boolean", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_CommunityPosts_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityPosts_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityVotes",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityVotes", x => new { x.PostId, x.UserId });
                    table.CheckConstraint("CK_CommunityVote_Value", "\"Value\" IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_CommunityVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityVotes_CommunityPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "CommunityPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_AuthorId_RequestKey",
                table: "CommunityPosts",
                columns: new[] { "AuthorId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_CourseId_CreatedAt",
                table: "CommunityPosts",
                columns: new[] { "CourseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_ParentId_CreatedAt",
                table: "CommunityPosts",
                columns: new[] { "ParentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityVotes_UserId",
                table: "CommunityVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityVotes");

            migrationBuilder.DropTable(
                name: "CommunityPosts");
        }
    }
}
