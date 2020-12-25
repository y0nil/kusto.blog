result <- df
n <- nrow(df)
g <- kargs$gain
f <- kargs$cycles
result$fx <- g * sin(df$x / n * 2 * pi * f)
