CREATE TABLE IF NOT EXISTS feedback (
    id          BIGINT       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    account     VARCHAR(64)  NOT NULL DEFAULT '',
    player_name VARCHAR(32)  NOT NULL DEFAULT '',
    kind        VARCHAR(16)  NOT NULL,
    priority    TINYINT      NOT NULL DEFAULT 2,
    title       VARCHAR(100) NOT NULL,
    message     TEXT         NOT NULL,
    context     VARCHAR(120) NOT NULL DEFAULT '',
    status      VARCHAR(16)  NOT NULL DEFAULT 'new',
    INDEX idx_feedback_status (status, created_at),
    INDEX idx_feedback_account (account)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_polish_ci;
