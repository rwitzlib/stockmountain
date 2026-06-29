# Use the official Node.js image as the base image
FROM node:24-slim

# Set the working directory inside the container
WORKDIR /app

# Copy the package.json and package-lock.json first to leverage Docker's cache
# This helps to avoid re-installing dependencies every time we change the source code
COPY package*.json ./

# Install the dependencies specified in package.json
RUN npm install

# Copy the rest of your project files into the container
COPY . .

# Expose the port the app runs on (optional, adjust as needed)
EXPOSE 8080

# Command to run the application
CMD ["npm", "run", "dev"]
