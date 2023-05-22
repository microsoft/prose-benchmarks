import sys
import json
import os
import requests
import argparse


def download_file(filename, url, output_directory):
    """
    Downloads a file from the given URL and saves it to the output directory.

    Args:
        filename (str): The name of the downloaded file. 
        url (str): The URL of the file to download.
        output_directory (str): The directory to save the downloaded file.
    """
    output_path = os.path.join(output_directory, filename)

    try:
        response = requests.get(url, stream=True)
        response.raise_for_status()

        with open(output_path, 'wb') as file:
            for chunk in response.iter_content(chunk_size=8192):
                file.write(chunk)

        print(f"Downloaded: {filename}")
    except requests.exceptions.RequestException as e:
        print(f"Error downloading {filename}: {str(e)}")


def main():
    """
    Main function to parse command-line arguments, read JSON file, and download files.
    """
    parser = argparse.ArgumentParser(description='Download files from a JSON dictionary.')
    parser.add_argument('--input', required=True, help='Path to the input JSON file.')
    parser.add_argument('--output', required=True, help='Path to the output directory.')
    args = parser.parse_args()

    input_file = args.input
    output_directory = args.output

    if not os.path.isfile(input_file):
        print(f"Input file '{input_file}' not found.")
        return

    if not os.path.isdir(output_directory):
        print(f"Output directory '{output_directory}' does not exist.")
        return

    with open(input_file) as file:
        data = json.load(file)

    for file_name, url in data.items():
        download_file(file_name, url, output_directory)


if __name__ == '__main__':
    main()
