using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudyEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudyJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    RequestJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceVersionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    Fence = table.Column<long>(type: "bigint", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ProviderCalls = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyJobs_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyJobs_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_StudyJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "StudyJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyCheckpoints",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyCheckpoints", x => new { x.JobId, x.Key });
                    table.ForeignKey(
                        name: "FK_StudyCheckpoints_StudyJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "StudyJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyRequestKeys",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyRequestKeys", x => new { x.OwnerId, x.Key });
                    table.ForeignKey(
                        name: "FK_StudyRequestKeys_StudyJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "StudyJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_JobId",
                table: "QuizAttempts",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_OwnerId_Key",
                table: "QuizAttempts",
                columns: new[] { "OwnerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyJobs_CourseId",
                table: "StudyJobs",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyJobs_OwnerId_Fingerprint",
                table: "StudyJobs",
                columns: new[] { "OwnerId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyJobs_State_NextAttemptAt",
                table: "StudyJobs",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequestKeys_JobId",
                table: "StudyRequestKeys",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizAttempts");

            migrationBuilder.DropTable(
                name: "StudyCheckpoints");

            migrationBuilder.DropTable(
                name: "StudyRequestKeys");

            migrationBuilder.DropTable(
                name: "StudyJobs");
        }
    }
}
