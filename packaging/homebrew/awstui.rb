class Awstui < Formula
  desc "Terminal UI for the AWS Console"
  homepage "https://github.com/mb-dot-dev/aws-console-tui"
  version "0.0.0"
  url "https://github.com/mb-dot-dev/aws-console-tui/releases/download/v0.0.0/awstui-0.0.0-osx-arm64.tar.gz"
  sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  depends_on macos: :big_sur
  depends_on arch: :arm64

  def install
    bin.install "awstui"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/awstui --version")
  end
end
