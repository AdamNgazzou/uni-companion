using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SavedTutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PracticeAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorConversations_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorConversations_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorTurns_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorTurns_StudyJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "StudyJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorTurns_TutorConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "TutorConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_CourseId",
                table: "TutorConversations",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_OwnerId_PracticeAttemptId",
                table: "TutorConversations",
                columns: new[] { "OwnerId", "PracticeAttemptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_OwnerId_RequestKey",
                table: "TutorConversations",
                columns: new[] { "OwnerId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorConversations_OwnerId_UpdatedAt_Id",
                table: "TutorConversations",
                columns: new[] { "OwnerId", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorTurns_ConversationId_Ordinal",
                table: "TutorTurns",
                columns: new[] { "ConversationId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorTurns_JobId",
                table: "TutorTurns",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorTurns_OwnerId_RequestKey",
                table: "TutorTurns",
                columns: new[] { "OwnerId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorTurns");

            migrationBuilder.DropTable(
                name: "TutorConversations");
        }
    }
}
