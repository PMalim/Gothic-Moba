CREATE TABLE IF NOT EXISTS profiles (
    uid            VARCHAR(64)  NOT NULL PRIMARY KEY,
    name           VARCHAR(64)  NOT NULL,
    points         INT          NOT NULL DEFAULT 0,
    lp             INT          NOT NULL DEFAULT 0,
    wins           INT          NOT NULL DEFAULT 0,
    losses         INT          NOT NULL DEFAULT 0,
    ranked_games   INT          NOT NULL DEFAULT 0,
    selected_hero  VARCHAR(32)  NOT NULL DEFAULT '',
    owned_heroes   VARCHAR(512) NOT NULL DEFAULT '',
    last_seen      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_profiles_lp (lp)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_polish_ci;
