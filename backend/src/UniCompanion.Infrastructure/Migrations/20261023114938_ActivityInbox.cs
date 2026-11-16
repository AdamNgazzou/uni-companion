using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActivityInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: true),
                    RecipientIds = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivityItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    GroupKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityItems_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityPreferences",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Replies = table.Column<bool>(type: "boolean", nullable: false),
                    FollowedThreads = table.Column<bool>(type: "boolean", nullable: false),
                    StudyJobs = table.Column<bool>(type: "boolean", nullable: false),
                    Upvotes = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityPreferences", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_ActivityPreferences_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_DispatchedAt_CreatedAt_Id",
                table: "ActivityEvents",
                columns: new[] { "DispatchedAt", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EventKey",
                table: "ActivityEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityItems_OwnerId_GroupKey",
                table: "ActivityItems",
                columns: new[] { "OwnerId", "GroupKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityItems_OwnerId_ReadAt_UpdatedAt_Id",
                table: "ActivityItems",
                columns: new[] { "OwnerId", "ReadAt", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityItems_OwnerId_UpdatedAt_Id",
                table: "ActivityItems",
                columns: new[] { "OwnerId", "UpdatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "ActivityItems");

            migrationBuilder.DropTable(
                name: "ActivityPreferences");
        }
    }
}
