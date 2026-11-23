/* =====================================================================
   TalentCore - Sistema de Gestion de Recursos Humanos (Grupo 6)
   Script de creacion de base de datos para SQL Server
   Fuente: Maqueta_Sistema_RRHH_Grupo6.pdf, paginas 11-15 (Modelo de datos)

   Bloques:
     1. Organizacion y empleados : Departamentos, Cargos, Empleados, Contratos, Evaluaciones
     2. Asistencia y ausencias   : RegistroAsistencia, TiposAusencia, SolicitudesAusencia, SaldoVacaciones
     3. Nomina y reclutamiento   : PeriodosNomina, RecibosNomina, DetalleRecibo, ConceptosNomina,
                                   Vacantes, Postulaciones, Candidatos
     4. Seguridad y auditoria    : Usuarios, Roles, UsuarioRoles, Auditoria

   Es re-ejecutable: si TalentCore existe, se elimina y se vuelve a crear.
   ===================================================================== */

-- Requerido por los indices filtrados (sqlcmd lo trae desactivado por defecto)
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE master;
GO

IF DB_ID(N'TalentCore') IS NOT NULL
BEGIN
    ALTER DATABASE TalentCore SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TalentCore;
END
GO

CREATE DATABASE TalentCore;
GO

USE TalentCore;
GO

/* =====================================================================
   1. ORGANIZACION Y EMPLEADOS
   ===================================================================== */

CREATE TABLE dbo.Departamentos (
    id_departamento INT           IDENTITY(1,1) NOT NULL,
    nombre          VARCHAR(100)  NOT NULL,
    id_responsable  INT           NULL,            -- FK a Empleados (se agrega mas abajo: referencia circular)
    activo          BIT           NOT NULL CONSTRAINT DF_Departamentos_activo DEFAULT (1),
    CONSTRAINT PK_Departamentos        PRIMARY KEY (id_departamento),
    CONSTRAINT UQ_Departamentos_nombre UNIQUE (nombre)
);
GO

CREATE TABLE dbo.Cargos (
    id_cargo        INT            IDENTITY(1,1) NOT NULL,
    nombre          VARCHAR(100)   NOT NULL,
    id_departamento INT            NOT NULL,
    salario_base    DECIMAL(12,2)  NOT NULL,
    CONSTRAINT PK_Cargos PRIMARY KEY (id_cargo),
    CONSTRAINT FK_Cargos_Departamentos FOREIGN KEY (id_departamento) REFERENCES dbo.Departamentos (id_departamento),
    CONSTRAINT UQ_Cargos_depto_nombre UNIQUE (id_departamento, nombre),
    CONSTRAINT CK_Cargos_salario_base CHECK (salario_base >= 0)
);
GO

CREATE TABLE dbo.Empleados (
    id_empleado      INT             IDENTITY(1,1) NOT NULL,
    codigo           VARCHAR(20)     NOT NULL,
    cedula_cifrada   VARBINARY(256)  NOT NULL,     -- campo sensible: cifrado en reposo
    nombres          VARCHAR(100)    NOT NULL,
    apellidos        VARCHAR(100)    NOT NULL,
    fecha_nacimiento DATE            NOT NULL,
    correo           VARCHAR(150)    NOT NULL,
    id_departamento  INT             NOT NULL,
    id_cargo         INT             NOT NULL,
    fecha_ingreso    DATE            NOT NULL,
    estado           VARCHAR(20)     NOT NULL CONSTRAINT DF_Empleados_estado DEFAULT ('Activo'),
    CONSTRAINT PK_Empleados PRIMARY KEY (id_empleado),
    CONSTRAINT UQ_Empleados_codigo UNIQUE (codigo),
    CONSTRAINT UQ_Empleados_correo UNIQUE (correo),
    CONSTRAINT FK_Empleados_Departamentos FOREIGN KEY (id_departamento) REFERENCES dbo.Departamentos (id_departamento),
    CONSTRAINT FK_Empleados_Cargos        FOREIGN KEY (id_cargo)        REFERENCES dbo.Cargos (id_cargo),
    CONSTRAINT CK_Empleados_fechas CHECK (fecha_ingreso >= fecha_nacimiento)
);
GO

-- Cierra la referencia circular Departamentos <-> Empleados
ALTER TABLE dbo.Departamentos
    ADD CONSTRAINT FK_Departamentos_Responsable
        FOREIGN KEY (id_responsable) REFERENCES dbo.Empleados (id_empleado);
GO

CREATE TABLE dbo.Contratos (
    id_contrato     INT             IDENTITY(1,1) NOT NULL,
    id_empleado     INT             NOT NULL,
    tipo            VARCHAR(30)     NOT NULL,
    fecha_inicio    DATE            NOT NULL,
    fecha_fin       DATE            NULL,           -- NULL = indefinido
    salario_cifrado VARBINARY(256)  NOT NULL,       -- campo sensible: cifrado en reposo
    activo          BIT             NOT NULL CONSTRAINT DF_Contratos_activo DEFAULT (1),
    CONSTRAINT PK_Contratos PRIMARY KEY (id_contrato),
    CONSTRAINT FK_Contratos_Empleados FOREIGN KEY (id_empleado) REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT CK_Contratos_fechas CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio)
);
GO

-- Un empleado solo puede tener un contrato activo ("crea un contrato y desactiva el anterior")
CREATE UNIQUE INDEX UX_Contratos_un_activo_por_empleado
    ON dbo.Contratos (id_empleado) WHERE activo = 1;
GO

CREATE TABLE dbo.Evaluaciones (
    id_evaluacion INT            IDENTITY(1,1) NOT NULL,
    id_empleado   INT            NOT NULL,
    id_evaluador  INT            NOT NULL,          -- relacion recursiva a Empleados
    periodo       VARCHAR(20)    NOT NULL,
    puntaje       DECIMAL(4,2)   NOT NULL,
    comentarios   NVARCHAR(MAX)  NULL,
    fecha         DATE           NOT NULL,
    CONSTRAINT PK_Evaluaciones PRIMARY KEY (id_evaluacion),
    CONSTRAINT FK_Evaluaciones_Empleado  FOREIGN KEY (id_empleado)  REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT FK_Evaluaciones_Evaluador FOREIGN KEY (id_evaluador) REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT CK_Evaluaciones_puntaje CHECK (puntaje >= 0),
    CONSTRAINT CK_Evaluaciones_distinto CHECK (id_empleado <> id_evaluador)
);
GO

/* =====================================================================
   2. ASISTENCIA Y AUSENCIAS
   ===================================================================== */

CREATE TABLE dbo.RegistroAsistencia (
    id_registro      BIGINT         IDENTITY(1,1) NOT NULL,
    id_empleado      INT            NOT NULL,
    fecha            DATE           NOT NULL,
    hora_entrada     TIME(0)        NULL,
    hora_salida      TIME(0)        NULL,
    horas_trabajadas DECIMAL(5,2)   NULL,
    origen           VARCHAR(20)    NOT NULL,       -- p. ej. Web, Kiosco, Manual
    estado           VARCHAR(20)    NOT NULL,
    CONSTRAINT PK_RegistroAsistencia PRIMARY KEY (id_registro),
    CONSTRAINT FK_RegistroAsistencia_Empleados FOREIGN KEY (id_empleado) REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT CK_RegistroAsistencia_horas CHECK (hora_salida IS NULL OR hora_entrada IS NULL OR hora_salida >= hora_entrada),
    CONSTRAINT CK_RegistroAsistencia_trabajadas CHECK (horas_trabajadas IS NULL OR (horas_trabajadas >= 0 AND horas_trabajadas <= 24))
);
GO

CREATE INDEX IX_RegistroAsistencia_empleado_fecha ON dbo.RegistroAsistencia (id_empleado, fecha);
CREATE INDEX IX_RegistroAsistencia_fecha          ON dbo.RegistroAsistencia (fecha);
GO

CREATE TABLE dbo.TiposAusencia (
    id_tipo             INT          IDENTITY(1,1) NOT NULL,
    nombre              VARCHAR(60)  NOT NULL,
    es_remunerada       BIT          NOT NULL,
    requiere_aprobacion BIT          NOT NULL,
    dias_maximos        INT          NULL,
    CONSTRAINT PK_TiposAusencia PRIMARY KEY (id_tipo),
    CONSTRAINT UQ_TiposAusencia_nombre UNIQUE (nombre),
    CONSTRAINT CK_TiposAusencia_dias CHECK (dias_maximos IS NULL OR dias_maximos > 0)
);
GO

CREATE TABLE dbo.SolicitudesAusencia (
    id_solicitud     INT            IDENTITY(1,1) NOT NULL,
    id_empleado      INT            NOT NULL,
    id_tipo          INT            NOT NULL,
    fecha_inicio     DATE           NOT NULL,
    fecha_fin        DATE           NOT NULL,
    dias_solicitados DECIMAL(5,1)   NOT NULL,       -- descuenta fines de semana y feriados (lo calcula la API)
    estado           VARCHAR(20)    NOT NULL CONSTRAINT DF_SolicitudesAusencia_estado DEFAULT ('Pendiente'),
    id_aprobador     INT            NULL,           -- jefe directo o usuario de RR. HH. (se valida en la API)
    motivo           NVARCHAR(500)  NULL,
    fecha_solicitud  DATETIME2(0)   NOT NULL CONSTRAINT DF_SolicitudesAusencia_fecha DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_SolicitudesAusencia PRIMARY KEY (id_solicitud),
    CONSTRAINT FK_SolicitudesAusencia_Empleado  FOREIGN KEY (id_empleado)  REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT FK_SolicitudesAusencia_Tipo      FOREIGN KEY (id_tipo)      REFERENCES dbo.TiposAusencia (id_tipo),
    CONSTRAINT FK_SolicitudesAusencia_Aprobador FOREIGN KEY (id_aprobador) REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT CK_SolicitudesAusencia_fechas CHECK (fecha_fin >= fecha_inicio),
    CONSTRAINT CK_SolicitudesAusencia_dias   CHECK (dias_solicitados > 0)
);
GO

CREATE INDEX IX_SolicitudesAusencia_empleado_fechas ON dbo.SolicitudesAusencia (id_empleado, fecha_inicio, fecha_fin);
CREATE INDEX IX_SolicitudesAusencia_estado          ON dbo.SolicitudesAusencia (estado);
GO

CREATE TABLE dbo.SaldoVacaciones (
    id_saldo       INT           IDENTITY(1,1) NOT NULL,
    id_empleado    INT           NOT NULL,
    anio           SMALLINT      NOT NULL,
    dias_asignados DECIMAL(5,1)  NOT NULL,
    dias_usados    DECIMAL(5,1)  NOT NULL CONSTRAINT DF_SaldoVacaciones_usados DEFAULT (0),
    CONSTRAINT PK_SaldoVacaciones PRIMARY KEY (id_saldo),
    CONSTRAINT FK_SaldoVacaciones_Empleados FOREIGN KEY (id_empleado) REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT UQ_SaldoVacaciones_empleado_anio UNIQUE (id_empleado, anio),
    CONSTRAINT CK_SaldoVacaciones_dias CHECK (dias_asignados >= 0 AND dias_usados >= 0 AND dias_usados <= dias_asignados)
);
GO

/* =====================================================================
   3. NOMINA Y RECLUTAMIENTO
   ===================================================================== */

CREATE TABLE dbo.PeriodosNomina (
    id_periodo INT          IDENTITY(1,1) NOT NULL,
    anio       SMALLINT     NOT NULL,
    mes        TINYINT      NOT NULL,
    tipo       VARCHAR(20)  NOT NULL,
    fecha_pago DATE         NULL,
    estado     VARCHAR(20)  NOT NULL CONSTRAINT DF_PeriodosNomina_estado DEFAULT ('Abierto'),
    CONSTRAINT PK_PeriodosNomina PRIMARY KEY (id_periodo),
    CONSTRAINT UQ_PeriodosNomina_anio_mes_tipo UNIQUE (anio, mes, tipo),
    CONSTRAINT CK_PeriodosNomina_mes CHECK (mes BETWEEN 1 AND 12)
);
GO

CREATE TABLE dbo.ConceptosNomina (
    id_concepto   INT           IDENTITY(1,1) NOT NULL,
    nombre        VARCHAR(80)   NOT NULL,
    tipo          VARCHAR(10)   NOT NULL,           -- Ingreso / Descuento
    porcentaje    DECIMAL(5,2)  NULL,
    es_obligatorio BIT          NOT NULL CONSTRAINT DF_ConceptosNomina_oblig DEFAULT (0),
    CONSTRAINT PK_ConceptosNomina PRIMARY KEY (id_concepto),
    CONSTRAINT UQ_ConceptosNomina_nombre UNIQUE (nombre),
    CONSTRAINT CK_ConceptosNomina_porcentaje CHECK (porcentaje IS NULL OR porcentaje BETWEEN 0 AND 100)
);
GO

CREATE TABLE dbo.RecibosNomina (
    id_recibo        BIGINT         IDENTITY(1,1) NOT NULL,
    id_periodo       INT            NOT NULL,
    id_empleado      INT            NOT NULL,
    salario_bruto    DECIMAL(12,2)  NOT NULL,
    total_ingresos   DECIMAL(12,2)  NOT NULL CONSTRAINT DF_RecibosNomina_ingresos   DEFAULT (0),
    total_descuentos DECIMAL(12,2)  NOT NULL CONSTRAINT DF_RecibosNomina_descuentos DEFAULT (0),
    salario_neto     DECIMAL(12,2)  NOT NULL,
    fecha_calculo    DATETIME2(0)   NOT NULL CONSTRAINT DF_RecibosNomina_fecha DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_RecibosNomina PRIMARY KEY (id_recibo),
    CONSTRAINT FK_RecibosNomina_Periodos  FOREIGN KEY (id_periodo)  REFERENCES dbo.PeriodosNomina (id_periodo),
    CONSTRAINT FK_RecibosNomina_Empleados FOREIGN KEY (id_empleado) REFERENCES dbo.Empleados (id_empleado),
    CONSTRAINT UQ_RecibosNomina_periodo_empleado UNIQUE (id_periodo, id_empleado)
);
GO

CREATE INDEX IX_RecibosNomina_empleado ON dbo.RecibosNomina (id_empleado);
GO

CREATE TABLE dbo.DetalleRecibo (
    id_detalle  BIGINT         IDENTITY(1,1) NOT NULL,
    id_recibo   BIGINT         NOT NULL,
    id_concepto INT            NOT NULL,
    monto       DECIMAL(12,2)  NOT NULL,
    CONSTRAINT PK_DetalleRecibo PRIMARY KEY (id_detalle),
    CONSTRAINT FK_DetalleRecibo_Recibos   FOREIGN KEY (id_recibo)   REFERENCES dbo.RecibosNomina (id_recibo),
    CONSTRAINT FK_DetalleRecibo_Conceptos FOREIGN KEY (id_concepto) REFERENCES dbo.ConceptosNomina (id_concepto),
    CONSTRAINT UQ_DetalleRecibo_recibo_concepto UNIQUE (id_recibo, id_concepto)
);
GO

CREATE TABLE dbo.Vacantes (
    id_vacante        INT            IDENTITY(1,1) NOT NULL,
    id_cargo          INT            NOT NULL,
    titulo            VARCHAR(150)   NOT NULL,
    descripcion       NVARCHAR(MAX)  NULL,
    fecha_publicacion DATE           NOT NULL CONSTRAINT DF_Vacantes_fecha DEFAULT (CAST(SYSUTCDATETIME() AS DATE)),
    estado            VARCHAR(20)    NOT NULL CONSTRAINT DF_Vacantes_estado DEFAULT ('Abierta'),
    CONSTRAINT PK_Vacantes PRIMARY KEY (id_vacante),
    CONSTRAINT FK_Vacantes_Cargos FOREIGN KEY (id_cargo) REFERENCES dbo.Cargos (id_cargo)
);
GO

CREATE TABLE dbo.Candidatos (
    id_candidato INT           IDENTITY(1,1) NOT NULL,
    nombres      VARCHAR(100)  NOT NULL,
    apellidos    VARCHAR(100)  NOT NULL,
    correo       VARCHAR(150)  NOT NULL,
    telefono     VARCHAR(20)   NULL,
    cv_ruta      VARCHAR(300)  NULL,
    CONSTRAINT PK_Candidatos PRIMARY KEY (id_candidato),
    CONSTRAINT UQ_Candidatos_correo UNIQUE (correo)
);
GO

CREATE TABLE dbo.Postulaciones (
    id_postulacion    INT           IDENTITY(1,1) NOT NULL,
    id_vacante        INT           NOT NULL,
    id_candidato      INT           NOT NULL,
    fecha_postulacion DATETIME2(0)  NOT NULL CONSTRAINT DF_Postulaciones_fecha DEFAULT (SYSUTCDATETIME()),
    etapa             VARCHAR(30)   NOT NULL CONSTRAINT DF_Postulaciones_etapa DEFAULT ('Postulado'),
    calificacion      DECIMAL(4,2)  NULL,
    CONSTRAINT PK_Postulaciones PRIMARY KEY (id_postulacion),
    CONSTRAINT FK_Postulaciones_Vacantes   FOREIGN KEY (id_vacante)   REFERENCES dbo.Vacantes (id_vacante),
    CONSTRAINT FK_Postulaciones_Candidatos FOREIGN KEY (id_candidato) REFERENCES dbo.Candidatos (id_candidato),
    CONSTRAINT UQ_Postulaciones_vacante_candidato UNIQUE (id_vacante, id_candidato)
);
GO

CREATE INDEX IX_Postulaciones_vacante_etapa ON dbo.Postulaciones (id_vacante, etapa);
GO

/* =====================================================================
   4. SEGURIDAD Y AUDITORIA
   ===================================================================== */

CREATE TABLE dbo.Roles (
    id_rol      INT           IDENTITY(1,1) NOT NULL,
    nombre      VARCHAR(30)   NOT NULL,
    descripcion VARCHAR(200)  NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (id_rol),
    CONSTRAINT UQ_Roles_nombre UNIQUE (nombre)
);
GO

CREATE TABLE dbo.Usuarios (
    id_usuario     INT             IDENTITY(1,1) NOT NULL,
    id_empleado    INT             NULL,            -- 0..1 : un usuario puede no ser empleado (p. ej. Administrador)
    correo         VARCHAR(150)    NOT NULL,
    password_hash  VARBINARY(256)  NOT NULL,        -- Argon2id / PBKDF2; nunca texto plano
    activo         BIT             NOT NULL CONSTRAINT DF_Usuarios_activo DEFAULT (1),
    ultimo_acceso  DATETIME2(0)    NULL,
    CONSTRAINT PK_Usuarios PRIMARY KEY (id_usuario),
    CONSTRAINT UQ_Usuarios_correo UNIQUE (correo),
    CONSTRAINT FK_Usuarios_Empleados FOREIGN KEY (id_empleado) REFERENCES dbo.Empleados (id_empleado)
);
GO

-- Relacion 1 a 0..1 con Empleados
CREATE UNIQUE INDEX UX_Usuarios_id_empleado
    ON dbo.Usuarios (id_empleado) WHERE id_empleado IS NOT NULL;
GO

CREATE TABLE dbo.UsuarioRoles (
    id_usuario       INT           NOT NULL,
    id_rol           INT           NOT NULL,
    asignado_por     INT           NULL,            -- usuario que hizo la asignacion
    fecha_asignacion DATETIME2(0)  NOT NULL CONSTRAINT DF_UsuarioRoles_fecha DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UsuarioRoles PRIMARY KEY (id_usuario, id_rol),
    CONSTRAINT FK_UsuarioRoles_Usuarios  FOREIGN KEY (id_usuario)   REFERENCES dbo.Usuarios (id_usuario),
    CONSTRAINT FK_UsuarioRoles_Roles     FOREIGN KEY (id_rol)       REFERENCES dbo.Roles (id_rol),
    CONSTRAINT FK_UsuarioRoles_AsignadoPor FOREIGN KEY (asignado_por) REFERENCES dbo.Usuarios (id_usuario)
);
GO

CREATE TABLE dbo.Auditoria (
    id_auditoria    BIGINT         IDENTITY(1,1) NOT NULL,
    id_usuario      INT            NULL,            -- NULL = accion del sistema
    entidad         VARCHAR(60)    NOT NULL,
    id_registro     VARCHAR(50)    NOT NULL,
    accion          VARCHAR(20)    NOT NULL,        -- INSERT / UPDATE / DELETE / READ ...
    fecha_hora      DATETIME2(3)   NOT NULL CONSTRAINT DF_Auditoria_fecha DEFAULT (SYSUTCDATETIME()),
    ip              VARCHAR(45)    NULL,            -- 45 = largo maximo de IPv6
    valores_antes   NVARCHAR(MAX)  NULL,
    valores_despues NVARCHAR(MAX)  NULL,
    CONSTRAINT PK_Auditoria PRIMARY KEY (id_auditoria),
    CONSTRAINT FK_Auditoria_Usuarios FOREIGN KEY (id_usuario) REFERENCES dbo.Usuarios (id_usuario)
);
GO

CREATE INDEX IX_Auditoria_entidad_registro ON dbo.Auditoria (entidad, id_registro);
CREATE INDEX IX_Auditoria_usuario_fecha    ON dbo.Auditoria (id_usuario, fecha_hora);
GO

-- La bitacora es de solo insercion: se bloquea UPDATE y DELETE
CREATE TRIGGER dbo.TR_Auditoria_SoloInsercion
ON dbo.Auditoria
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, 'La tabla Auditoria es de solo insercion: no se permite actualizar ni eliminar registros.', 1;
END
GO

/* =====================================================================
   DATOS INICIALES (catalogo de roles segun el PDF, pagina 2)
   ===================================================================== */

INSERT INTO dbo.Roles (nombre, descripcion) VALUES
    ('Administrador', 'Configuracion, usuarios, catalogos y auditoria'),
    ('RRHH',          'Empleados, asistencia, nomina, reclutamiento y reportes'),
    ('Jefe',          'Su equipo; aprueba ausencias y registra evaluaciones'),
    ('Empleado',      'Su ficha, marcas de asistencia, solicitudes y recibos');
GO

PRINT 'Base de datos TalentCore creada correctamente (20 tablas).';
GO
