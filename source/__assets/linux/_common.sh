#!/usr/bin/env bash
#
# Shared helpers for the Linux packers. The glibc sysroot lives in linux-x64-sysroot.sh.

# <releases> carries the version Flathub and the software centres show, and only that: the packers
# rewrite it to the one being packed instead of letting a history pile up. A hand-written entry for
# this label (one carrying real release notes) is kept as it stands.
# Usage: eg_sync_metainfo_release <metainfo.xml> <label> <release type, may be empty>
eg_sync_metainfo_release() {
	local file="$1" label="$2" release_type="$3"
	local top count keep attr

	top="$(sed -n 's:.*<release version="\([^"]*\)".*:\1:p' "$file" | head -n 1)"
	count="$(grep -c '<release ' "$file")"
	if [[ "$top" == "$label" && "$count" -le 1 ]]; then
		echo "==> metainfo: <releases> already holds $label alone"
		return 0
	fi

	# its notes are worth more than a generated line, so an entry already on this label survives
	keep=0
	[[ "$top" == "$label" ]] && keep=1

	# AppStream knows stable, development and snapshot; a release type marks a pre-release
	attr=""
	[[ -n "$release_type" ]] && attr=' type="development"'

	echo "==> metainfo: <releases> set to $label alone (was ${top:-none}, $count listed)"
	awk -v label="$label" -v day="$(date +%F)" -v attr="$attr" -v keep="$keep" '
		/<releases>/ && !seen {
			print
			seen = 1
			inside = 1
			if (!keep) {
				printf "    <release version=\"%s\" date=\"%s\"%s>\n", label, day, attr
				print  "      <url type=\"details\">https://github.com/d2phap/ExifGlass/releases</url>"
				print  "    </release>"
				done = 1
			}
			next
		}
		inside && /<\/releases>/ { inside = 0; print; next }
		inside {
			# with keep, the first entry is copied through and the rest dropped
			if (!done) {
				print
				if ($0 ~ /<\/release>/ || $0 ~ /\/>[[:space:]]*$/) done = 1
			}
			next
		}
		{ print }
	' "$file" > "$file.part" && mv "$file.part" "$file"

	[[ "$(sed -n 's:.*<release version="\([^"]*\)".*:\1:p' "$file" | head -n 1)" == "$label" \
		&& "$(grep -c '<release ' "$file")" -eq 1 ]] \
		|| { echo "Error: could not set <releases> to $label alone in $file (no <releases> element?)" >&2; return 1; }
}
