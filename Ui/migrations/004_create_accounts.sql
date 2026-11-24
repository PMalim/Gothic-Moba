CREATE TABLE IF NOT EXISTS accounts (
    username      VARCHAR(32)  NOT NULL PRIMARY KEY,
    display_name  VARCHAR(32)  NOT NULL,
    pass_hash     VARBINARY(64) NOT NULL,
    salt          VARBINARY(32) NOT NULL,
    iterations    INT          NOT NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login    DATETIME     NULL,
    last_uid      VARCHAR(64)  NOT NULL DEFAULT ''
) CHARACTER SET utf8mb4 COLLATE utf8mb4_polish_ci;
