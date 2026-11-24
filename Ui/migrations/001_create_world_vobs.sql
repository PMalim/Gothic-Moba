CREATE TABLE IF NOT EXISTS world_vobs (
    id          INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    world       VARCHAR(128) NOT NULL,
    visual      VARCHAR(96)  NOT NULL,
    x           FLOAT        NOT NULL,
    y           FLOAT        NOT NULL,
    z           FLOAT        NOT NULL,
    yaw         FLOAT        NOT NULL DEFAULT 0,
    pitch       FLOAT        NOT NULL DEFAULT 0,
    roll        FLOAT        NOT NULL DEFAULT 0,
    scale       FLOAT        NOT NULL DEFAULT 1,
    collision   TINYINT(1)   NOT NULL DEFAULT 1,
    placed_by   VARCHAR(64)  NOT NULL,
    placed_uid  VARCHAR(64)  NOT NULL DEFAULT '',
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_world_vobs_world (world),
    INDEX idx_world_vobs_visual (visual)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_polish_ci;
