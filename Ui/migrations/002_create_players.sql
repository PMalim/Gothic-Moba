CREATE TABLE IF NOT EXISTS players (
    uid           VARCHAR(64)  NOT NULL PRIMARY KEY,
    name          VARCHAR(64)  NOT NULL,
    team          TINYINT      NOT NULL DEFAULT 0,
    kills         INT          NOT NULL DEFAULT 0,
    deaths        INT          NOT NULL DEFAULT 0,
    play_seconds  BIGINT       NOT NULL DEFAULT 0,
    last_world    VARCHAR(128) NULL,
    last_x        FLOAT        NULL,
    last_y        FLOAT        NULL,
    last_z        FLOAT        NULL,
    first_seen    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_players_name (name)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_polish_ci;
