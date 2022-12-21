import random
import time

# Start the timer
start_time = time.time()

# Create a list of matrices
matrices = []

# Create an empty matrix
matrix = []

# Set a number of matrices to generate
matricesToGenerate = 75

# Iterate over the rows
for i in range(matricesToGenerate):
    # Create an empty row
    row = []

    # Iterate over the columns
    for j in range(matricesToGenerate):
        # Add a random element to the row
        row.append(random.randint(0, 10))

    # Add the row to the matrix
    matrix.append(row)

    matrices.append(matrix)

# Create an empty result matrix
result = matrices[0]

# Iterate over the remaining matrices in the list
for i in range(1, len(matrices)):
    # Create an empty result matrix
    temp = []

    # Iterate over the rows in the result matrix
    for j in range(len(result)):
        # Create an empty row
        row = []

        # Iterate over the columns in the current matrix
        for k in range(len(matrices[i][0])):
            # Initialize the dot product to 0
            dot_product = 0

            # Iterate over the elements in the row and column
            for l in range(len(result[0])):
                # Calculate the dot product
                dot_product += result[j][l] * matrices[i][l][k]

            # Add the dot product to the row
            row.append(dot_product % 10)

        # Add the row to the result matrix
        temp.append(row)

    # Update the result matrix
    result = temp

# Stop the timer
end_time = time.time()

# Calculate the elapsed time
elapsed_time = end_time - start_time

# Print the result
print(result)

# Print the elapsed time
print("The elapsed time is " + str(elapsed_time) + " seconds")

