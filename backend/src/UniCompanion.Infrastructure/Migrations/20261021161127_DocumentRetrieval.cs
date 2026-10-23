using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Page = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.CheckConstraint("CK_Chunk_VectorSize", "cardinality(\"Embedding\") = 384");
                    table.ForeignKey(
                        name: "FK_DocumentChunks_MaterialVersions_MaterialVersionId",
                        column: x => x.MaterialVersionId,
                        principalTable: "MaterialVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_MaterialVersionId_Model_Ordinal",
                table: "DocumentChunks",
                columns: new[] { "MaterialVersionId", "Model", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentChunks");
        }
    }
}
