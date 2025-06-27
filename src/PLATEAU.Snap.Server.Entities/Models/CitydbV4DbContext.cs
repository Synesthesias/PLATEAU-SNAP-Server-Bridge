using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PLATEAU.Snap.Server.Entities.Models;

namespace PLATEAU.Snap.Server.Entities;

public partial class CitydbV4DbContext : DbContext
{
    public CitydbV4DbContext()
    {
    }

    public CitydbV4DbContext(DbContextOptions<CitydbV4DbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Building> Buildings { get; set; }

    public virtual DbSet<BuildingFace> BuildingFaces { get; set; }

    public virtual DbSet<CityBoundary> CityBoundaries { get; set; }

    public virtual DbSet<Cityobject> Cityobjects { get; set; }

    public virtual DbSet<Image> Images { get; set; }

    public virtual DbSet<ImageSurfaceRelation> ImageSurfaceRelations { get; set; }

    public virtual DbSet<RoofSurface> RoofSurfaces { get; set; }

    public virtual DbSet<SurfaceGeometry> SurfaceGeometries { get; set; }

    public virtual DbSet<SurfaceImage> SurfaceImages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("postgis")
            .HasPostgresExtension("postgis_raster");

        modelBuilder.Entity<Building>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("building_pk");

            entity.ToTable("building", "citydb");

            entity.HasIndex(e => e.Lod0FootprintId, "building_lod0footprint_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod0RoofprintId, "building_lod0roofprint_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod1MultiSurfaceId, "building_lod1msrf_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod1SolidId, "building_lod1solid_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod1TerrainIntersection, "building_lod1terr_spx").HasMethod("gist");

            entity.HasIndex(e => e.Lod2MultiCurve, "building_lod2curve_spx").HasMethod("gist");

            entity.HasIndex(e => e.Lod2MultiSurfaceId, "building_lod2msrf_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod2SolidId, "building_lod2solid_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod2TerrainIntersection, "building_lod2terr_spx").HasMethod("gist");

            entity.HasIndex(e => e.Lod3MultiCurve, "building_lod3curve_spx").HasMethod("gist");

            entity.HasIndex(e => e.Lod3MultiSurfaceId, "building_lod3msrf_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod3SolidId, "building_lod3solid_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod3TerrainIntersection, "building_lod3terr_spx").HasMethod("gist");

            entity.HasIndex(e => e.Lod4MultiCurve, "building_lod4curve_spx").HasMethod("gist");

            entity.HasIndex(e => e.Lod4MultiSurfaceId, "building_lod4msrf_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod4SolidId, "building_lod4solid_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lod4TerrainIntersection, "building_lod4terr_spx").HasMethod("gist");

            entity.HasIndex(e => e.ObjectclassId, "building_objectclass_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.BuildingParentId, "building_parent_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.BuildingRootId, "building_root_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.BuildingParentId).HasColumnName("building_parent_id");
            entity.Property(e => e.BuildingRootId).HasColumnName("building_root_id");
            entity.Property(e => e.Class)
                .HasMaxLength(256)
                .HasColumnName("class");
            entity.Property(e => e.ClassCodespace)
                .HasMaxLength(4000)
                .HasColumnName("class_codespace");
            entity.Property(e => e.Function)
                .HasMaxLength(1000)
                .HasColumnName("function");
            entity.Property(e => e.FunctionCodespace)
                .HasMaxLength(4000)
                .HasColumnName("function_codespace");
            entity.Property(e => e.Lod0FootprintId).HasColumnName("lod0_footprint_id");
            entity.Property(e => e.Lod0RoofprintId).HasColumnName("lod0_roofprint_id");
            entity.Property(e => e.Lod1MultiSurfaceId).HasColumnName("lod1_multi_surface_id");
            entity.Property(e => e.Lod1SolidId).HasColumnName("lod1_solid_id");
            entity.Property(e => e.Lod1TerrainIntersection)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod1_terrain_intersection");
            entity.Property(e => e.Lod2MultiCurve)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod2_multi_curve");
            entity.Property(e => e.Lod2MultiSurfaceId).HasColumnName("lod2_multi_surface_id");
            entity.Property(e => e.Lod2SolidId).HasColumnName("lod2_solid_id");
            entity.Property(e => e.Lod2TerrainIntersection)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod2_terrain_intersection");
            entity.Property(e => e.Lod3MultiCurve)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod3_multi_curve");
            entity.Property(e => e.Lod3MultiSurfaceId).HasColumnName("lod3_multi_surface_id");
            entity.Property(e => e.Lod3SolidId).HasColumnName("lod3_solid_id");
            entity.Property(e => e.Lod3TerrainIntersection)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod3_terrain_intersection");
            entity.Property(e => e.Lod4MultiCurve)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod4_multi_curve");
            entity.Property(e => e.Lod4MultiSurfaceId).HasColumnName("lod4_multi_surface_id");
            entity.Property(e => e.Lod4SolidId).HasColumnName("lod4_solid_id");
            entity.Property(e => e.Lod4TerrainIntersection)
                .HasColumnType("geometry(MultiLineStringZ,6697)")
                .HasColumnName("lod4_terrain_intersection");
            entity.Property(e => e.MeasuredHeight).HasColumnName("measured_height");
            entity.Property(e => e.MeasuredHeightUnit)
                .HasMaxLength(4000)
                .HasColumnName("measured_height_unit");
            entity.Property(e => e.ObjectclassId).HasColumnName("objectclass_id");
            entity.Property(e => e.RoofType)
                .HasMaxLength(256)
                .HasColumnName("roof_type");
            entity.Property(e => e.RoofTypeCodespace)
                .HasMaxLength(4000)
                .HasColumnName("roof_type_codespace");
            entity.Property(e => e.StoreyHeightsAboveGround)
                .HasMaxLength(4000)
                .HasColumnName("storey_heights_above_ground");
            entity.Property(e => e.StoreyHeightsAgUnit)
                .HasMaxLength(4000)
                .HasColumnName("storey_heights_ag_unit");
            entity.Property(e => e.StoreyHeightsBelowGround)
                .HasMaxLength(4000)
                .HasColumnName("storey_heights_below_ground");
            entity.Property(e => e.StoreyHeightsBgUnit)
                .HasMaxLength(4000)
                .HasColumnName("storey_heights_bg_unit");
            entity.Property(e => e.StoreysAboveGround)
                .HasPrecision(8)
                .HasColumnName("storeys_above_ground");
            entity.Property(e => e.StoreysBelowGround)
                .HasPrecision(8)
                .HasColumnName("storeys_below_ground");
            entity.Property(e => e.Usage)
                .HasMaxLength(1000)
                .HasColumnName("usage");
            entity.Property(e => e.UsageCodespace)
                .HasMaxLength(4000)
                .HasColumnName("usage_codespace");
            entity.Property(e => e.YearOfConstruction).HasColumnName("year_of_construction");
            entity.Property(e => e.YearOfDemolition).HasColumnName("year_of_demolition");

            entity.HasOne(d => d.BuildingParent).WithMany(p => p.InverseBuildingParent)
                .HasForeignKey(d => d.BuildingParentId)
                .HasConstraintName("building_parent_fk");

            entity.HasOne(d => d.BuildingRoot).WithMany(p => p.InverseBuildingRoot)
                .HasForeignKey(d => d.BuildingRootId)
                .HasConstraintName("building_root_fk");

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.Building)
                .HasForeignKey<Building>(d => d.Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("building_cityobject_fk");

            entity.HasOne(d => d.Lod0Footprint).WithMany(p => p.BuildingLod0Footprints)
                .HasForeignKey(d => d.Lod0FootprintId)
                .HasConstraintName("building_lod0footprint_fk");

            entity.HasOne(d => d.Lod0Roofprint).WithMany(p => p.BuildingLod0Roofprints)
                .HasForeignKey(d => d.Lod0RoofprintId)
                .HasConstraintName("building_lod0roofprint_fk");

            entity.HasOne(d => d.Lod1MultiSurface).WithMany(p => p.BuildingLod1MultiSurfaces)
                .HasForeignKey(d => d.Lod1MultiSurfaceId)
                .HasConstraintName("building_lod1msrf_fk");

            entity.HasOne(d => d.Lod1Solid).WithMany(p => p.BuildingLod1Solids)
                .HasForeignKey(d => d.Lod1SolidId)
                .HasConstraintName("building_lod1solid_fk");

            entity.HasOne(d => d.Lod2MultiSurface).WithMany(p => p.BuildingLod2MultiSurfaces)
                .HasForeignKey(d => d.Lod2MultiSurfaceId)
                .HasConstraintName("building_lod2msrf_fk");

            entity.HasOne(d => d.Lod2Solid).WithMany(p => p.BuildingLod2Solids)
                .HasForeignKey(d => d.Lod2SolidId)
                .HasConstraintName("building_lod2solid_fk");

            entity.HasOne(d => d.Lod3MultiSurface).WithMany(p => p.BuildingLod3MultiSurfaces)
                .HasForeignKey(d => d.Lod3MultiSurfaceId)
                .HasConstraintName("building_lod3msrf_fk");

            entity.HasOne(d => d.Lod3Solid).WithMany(p => p.BuildingLod3Solids)
                .HasForeignKey(d => d.Lod3SolidId)
                .HasConstraintName("building_lod3solid_fk");

            entity.HasOne(d => d.Lod4MultiSurface).WithMany(p => p.BuildingLod4MultiSurfaces)
                .HasForeignKey(d => d.Lod4MultiSurfaceId)
                .HasConstraintName("building_lod4msrf_fk");

            entity.HasOne(d => d.Lod4Solid).WithMany(p => p.BuildingLod4Solids)
                .HasForeignKey(d => d.Lod4SolidId)
                .HasConstraintName("building_lod4solid_fk");
        });

        modelBuilder.Entity<BuildingFace>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("building_faces", "citydb");

            entity.Property(e => e.BuildingId).HasColumnName("building_id");
            entity.Property(e => e.Coordinates).HasColumnName("coordinates");
            entity.Property(e => e.FaceId).HasColumnName("face_id");
            entity.Property(e => e.Gmlid)
                .HasMaxLength(256)
                .HasColumnName("gmlid");
            entity.Property(e => e.ImageId).HasColumnName("image_id");
            entity.Property(e => e.IsOrtho).HasColumnName("is_ortho");
            entity.Property(e => e.Thumbnail).HasColumnName("thumbnail");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
        });

        modelBuilder.Entity<CityBoundary>(entity =>
        {
            entity.HasKey(e => e.Fid).HasName("city_boundary_pkey");

            entity.ToTable("city_boundary", "citydb");

            entity.HasIndex(e => e.Geom, "city_boundary_geom_geom_idx").HasMethod("gist");

            entity.Property(e => e.Fid)
                .HasDefaultValueSql("nextval('city_boundary_fid_seq'::regclass)")
                .HasColumnName("fid");
            entity.Property(e => e.Area).HasColumnName("area");
            entity.Property(e => e.AreaCode)
                .HasColumnType("character varying")
                .HasColumnName("area_code");
            entity.Property(e => e.CityName)
                .HasColumnType("character varying")
                .HasColumnName("city_name");
            entity.Property(e => e.CssName)
                .HasColumnType("character varying")
                .HasColumnName("css_name");
            entity.Property(e => e.Geom)
                .HasColumnType("geometry(Geometry,4326)")
                .HasColumnName("geom");
            entity.Property(e => e.GstCssName)
                .HasColumnType("character varying")
                .HasColumnName("gst_css_name");
            entity.Property(e => e.GstName)
                .HasColumnType("character varying")
                .HasColumnName("gst_name");
            entity.Property(e => e.PrefName)
                .HasColumnType("character varying")
                .HasColumnName("pref_name");
            entity.Property(e => e.SystemNumber)
                .HasColumnType("character varying")
                .HasColumnName("system_number");
            entity.Property(e => e.XCode).HasColumnName("x_code");
            entity.Property(e => e.YCode).HasColumnName("y_code");
        });

        modelBuilder.Entity<Cityobject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cityobject_pk");

            entity.ToTable("cityobject", "citydb");

            entity.HasIndex(e => e.CreationDate, "cityobj_creation_date_inx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.LastModificationDate, "cityobj_last_mod_date_inx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.TerminationDate, "cityobj_term_date_inx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Envelope, "cityobject_envelope_spx").HasMethod("gist");

            entity.HasIndex(e => new { e.Gmlid, e.GmlidCodespace }, "cityobject_inx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Lineage, "cityobject_lineage_inx");

            entity.HasIndex(e => e.ObjectclassId, "cityobject_objectclass_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('cityobject_seq'::regclass)")
                .HasColumnName("id");
            entity.Property(e => e.CreationDate).HasColumnName("creation_date");
            entity.Property(e => e.Description)
                .HasMaxLength(4000)
                .HasColumnName("description");
            entity.Property(e => e.Envelope)
                .HasColumnType("geometry(PolygonZ,6697)")
                .HasColumnName("envelope");
            entity.Property(e => e.Gmlid)
                .HasMaxLength(256)
                .HasColumnName("gmlid");
            entity.Property(e => e.GmlidCodespace)
                .HasMaxLength(1000)
                .HasColumnName("gmlid_codespace");
            entity.Property(e => e.LastModificationDate).HasColumnName("last_modification_date");
            entity.Property(e => e.Lineage)
                .HasMaxLength(256)
                .HasColumnName("lineage");
            entity.Property(e => e.Name)
                .HasMaxLength(1000)
                .HasColumnName("name");
            entity.Property(e => e.NameCodespace)
                .HasMaxLength(4000)
                .HasColumnName("name_codespace");
            entity.Property(e => e.ObjectclassId).HasColumnName("objectclass_id");
            entity.Property(e => e.ReasonForUpdate)
                .HasMaxLength(4000)
                .HasColumnName("reason_for_update");
            entity.Property(e => e.RelativeToTerrain)
                .HasMaxLength(256)
                .HasColumnName("relative_to_terrain");
            entity.Property(e => e.RelativeToWater)
                .HasMaxLength(256)
                .HasColumnName("relative_to_water");
            entity.Property(e => e.TerminationDate).HasColumnName("termination_date");
            entity.Property(e => e.UpdatingPerson)
                .HasMaxLength(256)
                .HasColumnName("updating_person");
            entity.Property(e => e.XmlSource).HasColumnName("xml_source");
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("images_pkey");

            entity.ToTable("images", "citydb");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('images_id_seq'::regclass)")
                .HasColumnName("id");
            entity.Property(e => e.Coordinates)
                .HasColumnType("geometry(Polygon)")
                .HasColumnName("coordinates");
            entity.Property(e => e.FromAltitude).HasColumnName("from_altitude");
            entity.Property(e => e.FromLatitude).HasColumnName("from_latitude");
            entity.Property(e => e.FromLongitude).HasColumnName("from_longitude");
            entity.Property(e => e.Roll).HasColumnName("roll");
            entity.Property(e => e.Thumbnail).HasColumnName("thumbnail");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("timestamp");
            entity.Property(e => e.ToAltitude).HasColumnName("to_altitude");
            entity.Property(e => e.ToLatitude).HasColumnName("to_latitude");
            entity.Property(e => e.ToLongitude).HasColumnName("to_longitude");
            entity.Property(e => e.Uri)
                .HasMaxLength(256)
                .HasColumnName("uri");
        });

        modelBuilder.Entity<ImageSurfaceRelation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("image_surface_relations_pkey");

            entity.ToTable("image_surface_relations", "citydb");

            entity.HasIndex(e => new { e.ImageId, e.Gmlid }, "image_surface_relations_image_id_gmlid_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('image_surface_relations_id_seq'::regclass)")
                .HasColumnName("id");
            entity.Property(e => e.Gmlid)
                .HasMaxLength(256)
                .HasColumnName("gmlid");
            entity.Property(e => e.ImageId).HasColumnName("image_id");

            entity.HasOne(d => d.Image).WithMany(p => p.ImageSurfaceRelations)
                .HasForeignKey(d => d.ImageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("image_surface_relations_image_id_fkey");
        });

        modelBuilder.Entity<RoofSurface>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("roof_surfaces", "citydb");

            entity.Property(e => e.BuildingId).HasColumnName("building_id");
            entity.Property(e => e.FaceId).HasColumnName("face_id");
            entity.Property(e => e.Gmlid)
                .HasMaxLength(256)
                .HasColumnName("gmlid");
        });

        modelBuilder.Entity<SurfaceGeometry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("surface_geometry_pk");

            entity.ToTable("surface_geometry", "citydb");

            entity.HasIndex(e => e.CityobjectId, "surface_geom_cityobj_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => new { e.Gmlid, e.GmlidCodespace }, "surface_geom_inx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.ParentId, "surface_geom_parent_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.RootId, "surface_geom_root_fkx").HasAnnotation("Npgsql:StorageParameter:fillfactor", "90");

            entity.HasIndex(e => e.Geometry, "surface_geom_spx").HasMethod("gist");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("nextval('surface_geometry_seq'::regclass)")
                .HasColumnName("id");
            entity.Property(e => e.CityobjectId).HasColumnName("cityobject_id");
            entity.Property(e => e.Geometry)
                .HasColumnType("geometry(PolygonZ,6697)")
                .HasColumnName("geometry");
            entity.Property(e => e.Gmlid)
                .HasMaxLength(256)
                .HasColumnName("gmlid");
            entity.Property(e => e.GmlidCodespace)
                .HasMaxLength(1000)
                .HasColumnName("gmlid_codespace");
            entity.Property(e => e.ImplicitGeometry)
                .HasColumnType("geometry(PolygonZ)")
                .HasColumnName("implicit_geometry");
            entity.Property(e => e.IsComposite).HasColumnName("is_composite");
            entity.Property(e => e.IsReverse).HasColumnName("is_reverse");
            entity.Property(e => e.IsSolid).HasColumnName("is_solid");
            entity.Property(e => e.IsTriangulated).HasColumnName("is_triangulated");
            entity.Property(e => e.IsXlink).HasColumnName("is_xlink");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.RootId).HasColumnName("root_id");

            entity.HasOne(d => d.Cityobject).WithMany(p => p.SurfaceGeometries)
                .HasForeignKey(d => d.CityobjectId)
                .HasConstraintName("surface_geom_cityobj_fk");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("surface_geom_parent_fk");

            entity.HasOne(d => d.Root).WithMany(p => p.InverseRoot)
                .HasForeignKey(d => d.RootId)
                .HasConstraintName("surface_geom_root_fk");
        });

        modelBuilder.Entity<SurfaceImage>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("surface_images", "citydb");

            entity.Property(e => e.BuildingId).HasColumnName("building_id");
            entity.Property(e => e.Center).HasColumnName("center");
            entity.Property(e => e.Coordinates)
                .HasColumnType("geometry(Polygon)")
                .HasColumnName("coordinates");
            entity.Property(e => e.FaceId).HasColumnName("face_id");
            entity.Property(e => e.Gmlid)
                .HasMaxLength(256)
                .HasColumnName("gmlid");
            entity.Property(e => e.ImageId).HasColumnName("image_id");
            entity.Property(e => e.Thumbnail).HasColumnName("thumbnail");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
        });
        modelBuilder.HasSequence("address_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("ade_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("appearance_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("citymodel_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("cityobject_genericatt_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("cityobject_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("external_ref_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("grid_coverage_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("implicit_geometry_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("schema_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("surface_data_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("surface_geometry_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);
        modelBuilder.HasSequence("tex_image_seq", "citydb")
            .HasMin(0L)
            .HasMax(2147483647L);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
